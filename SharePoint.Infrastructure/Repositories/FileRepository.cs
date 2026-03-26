using Microsoft.EntityFrameworkCore;
using SharePoint.Application.Abstractions;
using SharePoint.Domain.Entities;

namespace SharePoint.Infrastructure.Persistence;

public class FileRepository : IFileRepository
{
    private readonly AppDbContext _dbContext;

    public FileRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FileItem> AddAsync(FileItem file, CancellationToken cancellationToken)
    {
        _dbContext.Files.Add(file);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return file;
    }

    public Task<FileItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Files
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<FileItem>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Files
            .Where(x => x.CreatedByUserId == userId)
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<FileItem>> GetByFolderAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        return await _dbContext.Files
            .Where(x => x.ParentFolderId == parentFolderId)
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<FileItem> UpdateAsync(FileItem file, CancellationToken cancellationToken)
    {
        _dbContext.Files.Update(file);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return file;
    }

    public async Task SoftDeleteAsync(Guid id, Guid modifiedByUserId, CancellationToken cancellationToken)
    {
        var file = await _dbContext.Files
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (file is null)
        {
            return;
        }

        file.IsDeleted = true;
        file.ModifiedAt = DateTime.UtcNow;
        file.ModifiedByUserId = modifiedByUserId;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<FileItem?> GetDeletedByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Files
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyCollection<FileItem>> GetDeletedByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Files
            .IgnoreQueryFilters()
            .Where(x => x.CreatedByUserId == userId && x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<FileItem>> GetDeletedByFolderAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        return await _dbContext.Files
            .IgnoreQueryFilters()
            .Where(x => x.ParentFolderId == parentFolderId && x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task DeletePermanentlyAsync(Guid id, CancellationToken cancellationToken)
    {
        var file = await _dbContext.Files
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (file is null)
        {
            return;
        }

        _dbContext.Files.Remove(file);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid id, Guid modifiedByUserId, CancellationToken cancellationToken)
    {
        var file = await _dbContext.Files
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (file is null)
        {
            return;
        }

        file.IsDeleted = false;
        file.ModifiedAt = DateTime.UtcNow;
        file.ModifiedByUserId = modifiedByUserId;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
