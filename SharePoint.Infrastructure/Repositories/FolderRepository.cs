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

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return;
        }

        var folder = await _dbContext.Folders
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (folder is null)
        {
            return;
        }

        folder.IsDeleted = true;
        folder.ModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
