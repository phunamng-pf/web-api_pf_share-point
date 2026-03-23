using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Domain.Entities;

namespace SharePoint.Application.Services;

public sealed class FileService : IFileService
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

    public async Task<FileItemDto> UploadFileAsync(UploadFileRequest request, CancellationToken cancellationToken)
    {
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
        return new FileItemDto(saved.Id, saved.Name, saved.Extension, saved.ContentType, saved.SizeInBytes, saved.ParentFolderId, saved.CreatedAtUtc);
    }

    public async Task<IReadOnlyCollection<FileItemDto>> GetFilesAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        await ValidateParentFolderAccessAsync(parentFolderId, cancellationToken);

        var files = await _fileRepository.GetByFolderAsync(parentFolderId, cancellationToken);
        return files.Select(f => new FileItemDto(f.Id, f.Name, f.Extension, f.ContentType, f.SizeInBytes, f.ParentFolderId, f.CreatedAtUtc)).ToArray();
    }

    public async Task<(Stream Stream, FileItemDto File)> DownloadFileAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken)
                   ?? throw new FileNotFoundException($"File '{fileId}' not found.");

        VerifyUserAccess(file.CreatedByUserId);

        var stream = await _fileStorage.OpenReadAsync(file.StoragePath, cancellationToken)
                     ?? throw new FileNotFoundException($"Storage path '{file.StoragePath}' not found.");

        var dto = new FileItemDto(file.Id, file.Name, file.Extension, file.ContentType, file.SizeInBytes, file.ParentFolderId, file.CreatedAtUtc);
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
}
