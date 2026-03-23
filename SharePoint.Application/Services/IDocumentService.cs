using SharePoint.Application.Contracts;

namespace SharePoint.Application.Services;

public interface IDocumentService
{
    Task<FolderDto> CreateFolderAsync(CreateFolderRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FolderDto>> GetFoldersAsync(Guid? parentId, CancellationToken cancellationToken);
    Task<FileItemDto> UploadFileAsync(UploadFileRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FileItemDto>> GetFilesAsync(Guid? parentFolderId, CancellationToken cancellationToken);
    Task<(Stream Stream, FileItemDto File)> DownloadFileAsync(Guid fileId, CancellationToken cancellationToken);
}
