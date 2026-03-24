using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Request;
using SharePoint.Application.Contracts.Response;

namespace SharePoint.Application.Abstractions;

public interface IFileService
{
    Task<FileItemViewDto> CreateFileMetadataAsync(ReqCreateFileDto request, CancellationToken cancellationToken);
    Task<FileItemViewDto> UploadFileAsync(ReqUploadFileDto request, CancellationToken cancellationToken);
    Task<FileItemViewDto> UpdateFileAsync(ReqGuidNameDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FileItemViewDto>> GetFilesAsync(Guid? parentFolderId, CancellationToken cancellationToken);
    Task<(Stream Stream, FileItemViewDto File)> DownloadFileAsync(Guid fileId, CancellationToken cancellationToken);
    Task DeleteFileAsync(Guid fileId, CancellationToken cancellationToken);
}
