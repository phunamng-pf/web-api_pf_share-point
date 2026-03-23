using SharePoint.Application.Contracts;

namespace SharePoint.Application.Abstractions;

public interface IFolderService
{
    Task<FolderDto> CreateFolderAsync(CreateFolderRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FolderDto>> GetFoldersAsync(Guid? parentId, CancellationToken cancellationToken);
    Task DeleteFolderAsync(Guid folderId, CancellationToken cancellationToken);
}
