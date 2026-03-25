using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Request;
using SharePoint.Application.Contracts.Response;
using SharePoint.Domain.Entities;
using SharePoint.Application.Helper;

namespace SharePoint.Application.Services;

public class FileService : IFileService
{
    private static readonly Guid RootFolderId = Guid.Empty;

    private readonly IFolderRepository _folderRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IUserRepository _userRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IUserContext _userContext;

    public FileService(
        IFolderRepository folderRepository,
        IFileRepository fileRepository,
        IUserRepository userRepository,
        IFileStorage fileStorage,
        IUserContext userContext)
    {
        _folderRepository = folderRepository;
        _fileRepository = fileRepository;
        _userRepository = userRepository;
        _fileStorage = fileStorage;
        _userContext = userContext;
    }

    public async Task<FileItemViewDto> CreateFileMetadataAsync(ReqCreateFileDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("File name is required.", nameof(request.Name));
        }

        if (string.IsNullOrWhiteSpace(request.Extension))
        {
            throw new ArgumentException("File extension is required.", nameof(request.Extension));
        }

        var normalizedParentFolderId = NormalizeParentFolderId(request.ParentFolderId);
        await ValidateParentFolderAccessAsync(normalizedParentFolderId, cancellationToken);

        var normalizedExtension = request.Extension.StartsWith('.')
            ? request.Extension
            : $".{request.Extension}";
        var now = DateTime.UtcNow;

        var file = new FileItem
        {
            Name = request.Name.Trim(),
            Extension = normalizedExtension.Trim(),
            StoragePath = string.Empty,
            ContentType = "application/octet-stream",
            SizeInBytes = 0,
            ParentFolderId = normalizedParentFolderId,
            CreatedAt = now,
            ModifiedAt = now,
            CreatedByUserId = _userContext.UserId,
            ModifiedByUserId = _userContext.UserId
        };

        var saved = await _fileRepository.AddAsync(file, cancellationToken);
        var displayNameLookup = await BuildDisplayNameLookupAsync(new[] { saved }, cancellationToken);
        return DtoMappingHelper.MapFile(saved, displayNameLookup);
    }

    public async Task<FileItemViewDto> UploadFileAsync(ReqUploadFileDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException("File name is required.", nameof(request));
        }

        if (request.Content == Stream.Null)
        {
            throw new ArgumentException("File stream is required.", nameof(request));
        }

        var sizeInBytes = request.FileSize;
        if (sizeInBytes <= 0 && request.Content.CanSeek)
        {
            sizeInBytes = request.Content.Length;
        }

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("File content is empty.", nameof(request));
        }

        var normalizedParentFolderId = NormalizeParentFolderId(request.ParentFolderId);
        await ValidateParentFolderAccessAsync(normalizedParentFolderId, cancellationToken);

        var extension = Path.GetExtension(request.FileName);
        var storagePath = await _fileStorage.SaveAsync(request.Content, extension, cancellationToken);
        var now = DateTime.UtcNow;

        var file = new FileItem
        {
            Name = Path.GetFileNameWithoutExtension(request.FileName),
            Extension = extension,
            StoragePath = storagePath,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType)
                ? "application/octet-stream"
                : request.ContentType,
            SizeInBytes = sizeInBytes,
            ParentFolderId = normalizedParentFolderId,
            CreatedAt = now,
            ModifiedAt = now,
            CreatedByUserId = _userContext.UserId,
            ModifiedByUserId = _userContext.UserId
        };

        var saved = await _fileRepository.AddAsync(file, cancellationToken);
        var displayNameLookup = await BuildDisplayNameLookupAsync(new[] { saved }, cancellationToken);
        return DtoMappingHelper.MapFile(saved, displayNameLookup);
    }

    public async Task<FileItemViewDto> UpdateFileAsync(ReqGuidNameDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("File name is required.", nameof(request));
        }

        var file = await _fileRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new FileNotFoundException($"File '{request.Id}' not found.");

        VerifyUserAccess(file.CreatedByUserId);

        file.Name = request.Name.Trim();
        file.ModifiedAt = DateTime.UtcNow;
        file.ModifiedByUserId = _userContext.UserId;

        var updated = await _fileRepository.UpdateAsync(file, cancellationToken);
        var displayNameLookup = await BuildDisplayNameLookupAsync(new[] { updated }, cancellationToken);
        return DtoMappingHelper.MapFile(updated, displayNameLookup);
    }

    public async Task<IReadOnlyCollection<FileItemViewDto>> GetFilesAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        var normalizedParentFolderId = NormalizeParentFolderId(parentFolderId);
        await ValidateParentFolderAccessAsync(normalizedParentFolderId, cancellationToken);

        var files = await _fileRepository.GetByFolderAsync(normalizedParentFolderId, cancellationToken);
        var displayNameLookup = await BuildDisplayNameLookupAsync(files, cancellationToken);
        return files.Select(x => DtoMappingHelper.MapFile(x, displayNameLookup)).ToArray();
    }

    public async Task<(Stream Stream, FileItemViewDto File)> DownloadFileAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken)
                   ?? throw new FileNotFoundException($"File '{fileId}' not found.");

        VerifyUserAccess(file.CreatedByUserId);

        var stream = await _fileStorage.OpenReadAsync(file.StoragePath, cancellationToken)
                     ?? throw new FileNotFoundException($"Storage path '{file.StoragePath}' not found.");

        var displayNameLookup = await BuildDisplayNameLookupAsync(new[] { file }, cancellationToken);
        var dto = DtoMappingHelper.MapFile(file, displayNameLookup);
        return (stream, dto);
    }

    public async Task DeleteFileAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken)
                   ?? throw new FileNotFoundException($"File '{fileId}' not found.");

        VerifyUserAccess(file.CreatedByUserId);
        await _fileRepository.SoftDeleteAsync(fileId, cancellationToken);
    }

    /// <summary>
    /// Validates that the parent folder exists and that the current user has access to it.
    /// </summary>
    private async Task ValidateParentFolderAccessAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        var normalizedParentFolderId = NormalizeParentFolderId(parentFolderId);

        if (normalizedParentFolderId == RootFolderId)
        {
            return;
        }

        var parentFolder = await _folderRepository.GetByIdAsync(normalizedParentFolderId, cancellationToken)
                           ?? throw new FileNotFoundException($"Folder '{normalizedParentFolderId}' not found.");

        VerifyUserAccess(parentFolder.CreatedByUserId);
    }

    private static Guid NormalizeParentFolderId(Guid? parentFolderId)
    {
        return parentFolderId ?? RootFolderId;
    }

    /// <summary>
    /// Verifies that the resource owner (createdByUserId) matches the current user.
    /// Throws UnauthorizedAccessException if access is denied.
    /// </summary>
    private void VerifyUserAccess(Guid createdByUserId)
    {
        if (createdByUserId != _userContext.UserId)
        {
            throw new UnauthorizedAccessException("You do not have permission to access this resource.");
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> BuildDisplayNameLookupAsync(
        IEnumerable<FileItem> files,
        CancellationToken cancellationToken)
    {
        var userIds = new HashSet<Guid>();

        foreach (var file in files)
        {
            if (file.CreatedByUserId != Guid.Empty)
            {
                userIds.Add(file.CreatedByUserId);
            }

            if (file.ModifiedByUserId.HasValue && file.ModifiedByUserId.Value != Guid.Empty)
            {
                userIds.Add(file.ModifiedByUserId.Value);
            }
        }

        return await _userRepository.GetDisplayNamesByIdsAsync(userIds.ToArray(), cancellationToken);
    }
}
