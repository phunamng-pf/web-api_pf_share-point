using SharePoint.Domain.Entities;

namespace SharePoint.Application.Abstractions;

public interface IFileRepository
{
    Task<FileItem> AddAsync(FileItem file, CancellationToken cancellationToken);
    Task<FileItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FileItem>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FileItem>> GetByFolderAsync(Guid? parentFolderId, CancellationToken cancellationToken);
    Task<FileItem> UpdateAsync(FileItem file, CancellationToken cancellationToken);

    Task SoftDeleteAsync(Guid id, Guid modifiedByUserId, CancellationToken cancellationToken);
    Task<FileItem?> GetDeletedByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FileItem>> GetDeletedByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FileItem>> GetDeletedByFolderAsync(Guid? parentFolderId, CancellationToken cancellationToken);

    Task RestoreAsync(Guid id, Guid modifiedByUserId, CancellationToken cancellationToken);
    Task DeletePermanentlyAsync(Guid id, CancellationToken cancellationToken);
}
