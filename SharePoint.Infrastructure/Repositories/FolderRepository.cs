using Microsoft.EntityFrameworkCore;
using SharePoint.Application.Abstractions;
using SharePoint.Domain.Entities;

namespace SharePoint.Infrastructure.Persistence;

public class FolderRepository : IFolderRepository
{
    private readonly AppDbContext _dbContext;

    public FolderRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Folder> AddAsync(Folder folder, CancellationToken cancellationToken)
    {
        _dbContext.Folders.Add(folder);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return folder;
    }

    public Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Folders
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Folder>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Folders
            .Where(x => x.CreatedByUserId == userId || x.Id == Guid.Empty)
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Folder>> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Folders
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Folder> UpdateAsync(Folder folder, CancellationToken cancellationToken)
    {
        _dbContext.Folders.Update(folder);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return folder;
    }

    public async Task SoftDeleteAsync(Guid id, Guid modifiedByUserId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return;
        }

        var folder = await _dbContext.Folders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (folder is null)
        {
            return;
        }

        folder.IsDeleted = true;
        folder.ModifiedAt = DateTime.UtcNow;
        folder.ModifiedByUserId = modifiedByUserId;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Folder?> GetDeletedByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Folders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Folder>> GetDeletedByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Folders
            .IgnoreQueryFilters()
            .Where(x => x.CreatedByUserId == userId && x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Folder>> GetDeletedChildrenAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Folders
            .IgnoreQueryFilters()
            .Where(x => x.ParentId == parentId && x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid id, Guid modifiedByUserId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return;
        }

        var folder = await _dbContext.Folders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (folder is null)
        {
            return;
        }

        folder.IsDeleted = false;
        folder.ModifiedAt = DateTime.UtcNow;
        folder.ModifiedByUserId = modifiedByUserId;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePermanentlyAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return;
        }

        var folder = await _dbContext.Folders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (folder is null)
        {
            return;
        }

        _dbContext.Folders.Remove(folder);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
