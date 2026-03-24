using SharePoint.Domain.Entities;

namespace SharePoint.Application.Abstractions;

public interface IFolderRepository
{
    Task<Folder> AddAsync(Folder folder, CancellationToken cancellationToken);
    Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Folder>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Folder>> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken);
    Task<Folder> UpdateAsync(Folder folder, CancellationToken cancellationToken);
    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken);
}
