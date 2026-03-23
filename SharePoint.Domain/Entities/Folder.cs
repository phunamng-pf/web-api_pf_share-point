using SharePoint.Domain.Common;

namespace SharePoint.Domain.Entities;

public sealed class Folder : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public Guid? ParentId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}
