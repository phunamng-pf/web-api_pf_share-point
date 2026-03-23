using Microsoft.EntityFrameworkCore;
using SharePoint.Application.Abstractions;
using SharePoint.Domain.Entities;

namespace SharePoint.Infrastructure.Persistence;

public sealed class FileRepository : IFileRepository
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

    public async Task<IReadOnlyCollection<FileItem>> GetByFolderAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        return await _dbContext.Files
            .Where(x => x.ParentFolderId == parentFolderId)
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var file = await _dbContext.Files
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (file is null)
        {
            return;
        }

        file.IsDeleted = true;
        file.ModifiedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
