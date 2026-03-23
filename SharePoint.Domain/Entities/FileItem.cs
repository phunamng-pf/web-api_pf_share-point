using SharePoint.Domain.Common;

namespace SharePoint.Domain.Entities;

public sealed class FileItem : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Extension { get; set; }
    public required string StoragePath { get; set; }
    public required string ContentType { get; set; }
    public long SizeInBytes { get; set; }
    public Guid? ParentFolderId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}
