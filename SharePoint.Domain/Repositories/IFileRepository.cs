using SharePoint.Domain.Entities;

namespace SharePoint.Application.Abstractions;

public interface IFileRepository
{
    Task<FileItem> AddAsync(FileItem file, CancellationToken cancellationToken);
    Task<FileItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FileItem>> GetByFolderAsync(Guid? parentFolderId, CancellationToken cancellationToken);
    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken);
}
