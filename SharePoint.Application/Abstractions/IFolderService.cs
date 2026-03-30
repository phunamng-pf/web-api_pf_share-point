using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Request;
using SharePoint.Application.Contracts.Response;

namespace SharePoint.Application.Abstractions;

public interface IFolderService
{
    Task<FolderTreeDto> CreateFolderAsync(ReqCreateFolderDto request, CancellationToken cancellationToken);
    Task<FolderTreeDto> GetFolderByIdAsync(Guid folderId, CancellationToken cancellationToken);
    Task<FolderTreeDto> UpdateFolderAsync(ReqGuidNameDto request, CancellationToken cancellationToken);
    Task DeleteFolderAsync(Guid folderId, CancellationToken cancellationToken);
    Task RestoreFolderAsync(Guid folderId, CancellationToken cancellationToken);
    Task DeleteFolderPermanentlyAsync(Guid folderId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BreadcrumbInfoDto>> GetBreadcrumbAsync(Guid folderId, CancellationToken cancellationToken);
    Task<(Stream Stream, string FolderName)> DownloadFolderAsync(Guid folderId, CancellationToken cancellationToken);
}
