using SharePoint.Domain.Entities;

namespace SharePoint.Application.Abstractions;

public interface IFolderRepository
{
    Task<Folder> AddAsync(Folder folder, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Folder>> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken);
}
