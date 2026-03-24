using Microsoft.EntityFrameworkCore;
using SharePoint.Domain.Entities;

namespace SharePoint.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<FileItem> Files => Set<FileItem>();
    public DbSet<ItemPermission> Permissions => Set<ItemPermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<AppUser>();
        user.HasIndex(x => x.AzureAdObjectId).IsUnique();
        user.Property(x => x.AzureAdObjectId).HasMaxLength(128);
        user.Property(x => x.TenantId).HasMaxLength(128);
        user.Property(x => x.Email).HasMaxLength(320);
        user.Property(x => x.DisplayName).HasMaxLength(256);

        var folder = modelBuilder.Entity<Folder>();
        folder.HasOne(f => f.Parent).WithMany(f => f.SubFolders).HasForeignKey(f => f.ParentId).OnDelete(DeleteBehavior.Restrict);
        folder.HasMany(f => f.Files).WithOne(fi => fi.ParentFolder).HasForeignKey(fi => fi.ParentFolderId).OnDelete(DeleteBehavior.Restrict);
        folder.Property(x => x.Name).HasMaxLength(255);
        folder.HasQueryFilter(x => !x.IsDeleted);

        var file = modelBuilder.Entity<FileItem>();
        file.Property(x => x.Name).HasMaxLength(255);
        file.Property(x => x.Extension).HasMaxLength(32);
        file.Property(x => x.ContentType).HasMaxLength(255);
        file.Property(x => x.StoragePath).HasMaxLength(1024);
        file.HasQueryFilter(x => !x.IsDeleted);

        var permission = modelBuilder.Entity<ItemPermission>();
        permission.HasIndex(x => new { x.UserId, x.ItemId, x.ItemType }).IsUnique();
        permission.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
