using SharePoint.Domain.Entities;

namespace SharePoint.Application.Abstractions;

public interface IFolderRepository
{
    Task<Folder> AddAsync(Folder folder, CancellationToken cancellationToken);
    Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Folder>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Folder>> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken);
    Task<Folder> UpdateAsync(Folder folder, CancellationToken cancellationToken);

    Task SoftDeleteAsync(Guid id, Guid modifiedByUserId, CancellationToken cancellationToken);
    Task<Folder?> GetDeletedByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Folder>> GetDeletedByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Folder>> GetDeletedChildrenAsync(Guid? parentId, CancellationToken cancellationToken);

    Task RestoreAsync(Guid id, Guid modifiedByUserId, CancellationToken cancellationToken);
    Task DeletePermanentlyAsync(Guid id, CancellationToken cancellationToken);
}
