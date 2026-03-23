using System.Collections.Concurrent;
using SharePoint.Application.Abstractions;
using SharePoint.Domain.Entities;

namespace SharePoint.Infrastructure.Persistence;

public sealed class InMemoryFileRepository : IFileRepository
{
    private static readonly ConcurrentDictionary<Guid, FileItem> Data = new();

    public Task<FileItem> AddAsync(FileItem file, CancellationToken cancellationToken)
    {
        Data[file.Id] = file;
        return Task.FromResult(file);
    }

    public Task<FileItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Data.TryGetValue(id, out var file);
        return Task.FromResult(file?.IsDeleted == true ? null : file);
    }

    public Task<IReadOnlyCollection<FileItem>> GetByFolderAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        var result = Data.Values
            .Where(x => x.ParentFolderId == parentFolderId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<FileItem>>(result);
    }
}
