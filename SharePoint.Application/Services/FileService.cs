using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Request;
using SharePoint.Application.Contracts.Response;
using SharePoint.Domain.Entities;

namespace SharePoint.Application.Services;

public class FileService : IFileService
{
    private readonly IFolderRepository _folderRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IUserContext _userContext;

    public FileService(
        IFolderRepository folderRepository,
        IFileRepository fileRepository,
        IFileStorage fileStorage,
        IUserContext userContext)
    {
        _folderRepository = folderRepository;
        _fileRepository = fileRepository;
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

        await ValidateParentFolderAccessAsync(request.ParentFolderId, cancellationToken);

        var normalizedExtension = request.Extension.StartsWith('.')
            ? request.Extension
            : $".{request.Extension}";

        var file = new FileItem
        {
            Name = request.Name.Trim(),
            Extension = normalizedExtension.Trim(),
            StoragePath = string.Empty,
            ContentType = "application/octet-stream",
            SizeInBytes = 0,
            ParentFolderId = request.ParentFolderId,
            CreatedByUserId = _userContext.UserId
        };

        var saved = await _fileRepository.AddAsync(file, cancellationToken);
        return MapFile(saved);
    }

    public async Task<FileItemViewDto> UploadFileAsync(ReqUploadFileDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException("File name is required.", nameof(request));
        }

        if (request.Content.Length == 0)
        {
            throw new ArgumentException("File content is empty.", nameof(request));
        }

        await ValidateParentFolderAccessAsync(request.ParentFolderId, cancellationToken);

        var extension = Path.GetExtension(request.FileName);
        var storagePath = await _fileStorage.SaveAsync(request.Content, extension, cancellationToken);

        var file = new FileItem
        {
            Name = Path.GetFileNameWithoutExtension(request.FileName),
            Extension = extension,
            StoragePath = storagePath,
            ContentType = request.ContentType,
            SizeInBytes = request.Content.Length,
            ParentFolderId = request.ParentFolderId,
            CreatedByUserId = _userContext.UserId
        };

        var saved = await _fileRepository.AddAsync(file, cancellationToken);
        return MapFile(saved);
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
        file.ModifiedAtUtc = DateTime.UtcNow;
        file.ModifiedByUserId = _userContext.UserId;

        var updated = await _fileRepository.UpdateAsync(file, cancellationToken);
        return MapFile(updated);
    }

    public async Task<IReadOnlyCollection<FileItemViewDto>> GetFilesAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        await ValidateParentFolderAccessAsync(parentFolderId, cancellationToken);

        var files = await _fileRepository.GetByFolderAsync(parentFolderId, cancellationToken);
        return files.Select(MapFile).ToArray();
    }

    public async Task<(Stream Stream, FileItemViewDto File)> DownloadFileAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken)
                   ?? throw new FileNotFoundException($"File '{fileId}' not found.");

        VerifyUserAccess(file.CreatedByUserId);

        var stream = await _fileStorage.OpenReadAsync(file.StoragePath, cancellationToken)
                     ?? throw new FileNotFoundException($"Storage path '{file.StoragePath}' not found.");

        var dto = MapFile(file);
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
        if (!parentFolderId.HasValue)
        {
            return;
        }

        var parentFolder = await _folderRepository.GetByIdAsync(parentFolderId.Value, cancellationToken)
                           ?? throw new FileNotFoundException($"Folder '{parentFolderId.Value}' not found.");

        VerifyUserAccess(parentFolder.CreatedByUserId);
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

    private static FileItemViewDto MapFile(FileItem file)
    {
        return new FileItemViewDto
        {
            Id = file.Id.ToString(),
            Name = file.Name,
            Extension = file.Extension,
            ContentType = file.ContentType,
            SizeInBytes = file.SizeInBytes,
            CreatedAt = file.CreatedAtUtc,
            CreatedBy = file.CreatedByUserId.ToString(),
            ModifiedAt = file.ModifiedAtUtc,
            ModifiedBy = file.ModifiedByUserId?.ToString(),
            ParentFolderId = file.ParentFolderId?.ToString()
        };
    }
}
