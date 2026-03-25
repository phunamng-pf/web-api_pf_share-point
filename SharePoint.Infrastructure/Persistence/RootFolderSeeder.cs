using SharePoint.Domain.Entities;

namespace SharePoint.Infrastructure.Persistence;

public static class RootFolderSeeder
{
    public static async Task EnsureRootFolderAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var rootFolder = await dbContext.Folders.FindAsync([Guid.Empty], cancellationToken);
        if (rootFolder is not null)
        {
            if (rootFolder.IsDeleted)
            {
                rootFolder.IsDeleted = false;
                rootFolder.ModifiedAt = DateTime.UtcNow;
                rootFolder.ModifiedByUserId = null;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (!rootFolder.ModifiedAt.HasValue)
            {
                rootFolder.ModifiedAt = rootFolder.CreatedAt;
                rootFolder.ModifiedByUserId = rootFolder.CreatedByUserId;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var now = DateTime.UtcNow;

        dbContext.Folders.Add(new Folder
        {
            Id = Guid.Empty,
            Name = "Documents",
            ParentId = null,
            CreatedAt = now,
            ModifiedAt = now,
            CreatedByUserId = Guid.Empty,
            ModifiedByUserId = Guid.Empty,
            IsDeleted = false
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}