using SharePoint.Application.Contracts;

namespace SharePoint.Application.Abstractions;

public interface IFileService
{
    Task<FileItemDto> UploadFileAsync(UploadFileRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FileItemDto>> GetFilesAsync(Guid? parentFolderId, CancellationToken cancellationToken);
    Task<(Stream Stream, FileItemDto File)> DownloadFileAsync(Guid fileId, CancellationToken cancellationToken);
    Task DeleteFileAsync(Guid fileId, CancellationToken cancellationToken);
}
