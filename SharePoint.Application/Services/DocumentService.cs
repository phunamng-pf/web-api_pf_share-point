using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Domain.Entities;

namespace SharePoint.Application.Services;

public sealed class DocumentService(
    IFolderRepository folderRepository,
    IFileRepository fileRepository,
    IFileStorage fileStorage,
    IUserContext userContext) : IDocumentService
{
    public async Task<FolderDto> CreateFolderAsync(CreateFolderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Folder name is required.", nameof(request));
        }

        var folder = new Folder
        {
            Name = request.Name.Trim(),
            ParentId = request.ParentId,
            CreatedByUserId = userContext.UserId
        };

        var saved = await folderRepository.AddAsync(folder, cancellationToken);
        return new FolderDto(saved.Id, saved.Name, saved.ParentId, saved.CreatedAtUtc);
    }

    public async Task<IReadOnlyCollection<FolderDto>> GetFoldersAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        var folders = await folderRepository.GetChildrenAsync(parentId, cancellationToken);
        return folders
            .Select(f => new FolderDto(f.Id, f.Name, f.ParentId, f.CreatedAtUtc))
            .ToArray();
    }

    public async Task<FileItemDto> UploadFileAsync(UploadFileRequest request, CancellationToken cancellationToken)
    {
        if (request.Content.Length == 0)
        {
            throw new ArgumentException("File content is empty.", nameof(request));
        }

        var extension = Path.GetExtension(request.FileName);
        var storagePath = await fileStorage.SaveAsync(request.Content, extension, cancellationToken);

        var file = new FileItem
        {
            Name = Path.GetFileNameWithoutExtension(request.FileName),
            Extension = extension,
            StoragePath = storagePath,
            ContentType = request.ContentType,
            SizeInBytes = request.Content.Length,
            ParentFolderId = request.ParentFolderId,
            CreatedByUserId = userContext.UserId
        };

        var saved = await fileRepository.AddAsync(file, cancellationToken);
        return new FileItemDto(saved.Id, saved.Name, saved.Extension, saved.ContentType, saved.SizeInBytes, saved.ParentFolderId, saved.CreatedAtUtc);
    }

    public async Task<IReadOnlyCollection<FileItemDto>> GetFilesAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        var files = await fileRepository.GetByFolderAsync(parentFolderId, cancellationToken);
        return files
            .Select(f => new FileItemDto(f.Id, f.Name, f.Extension, f.ContentType, f.SizeInBytes, f.ParentFolderId, f.CreatedAtUtc))
            .ToArray();
    }

    public async Task<(Stream Stream, FileItemDto File)> DownloadFileAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var file = await fileRepository.GetByIdAsync(fileId, cancellationToken)
                   ?? throw new FileNotFoundException($"File '{fileId}' not found.");

        var stream = await fileStorage.OpenReadAsync(file.StoragePath, cancellationToken)
                     ?? throw new FileNotFoundException($"Storage path '{file.StoragePath}' not found.");

        var dto = new FileItemDto(file.Id, file.Name, file.Extension, file.ContentType, file.SizeInBytes, file.ParentFolderId, file.CreatedAtUtc);
        return (stream, dto);
    }
}
