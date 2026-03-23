using System.Collections.Concurrent;
using SharePoint.Application.Abstractions;
using SharePoint.Domain.Entities;

namespace SharePoint.Infrastructure.Persistence;

public sealed class InMemoryFolderRepository : IFolderRepository
{
    private static readonly ConcurrentDictionary<Guid, Folder> Data = new();

    public Task<Folder> AddAsync(Folder folder, CancellationToken cancellationToken)
    {
        Data[folder.Id] = folder;
        return Task.FromResult(folder);
    }

    public Task<IReadOnlyCollection<Folder>> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        var result = Data.Values
            .Where(x => x.ParentId == parentId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<Folder>>(result);
    }
}
