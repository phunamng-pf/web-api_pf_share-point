using SharePoint.Domain.Common;

namespace SharePoint.Domain.Entities;

public class Folder : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public Guid? ParentId { get; set; }
    public Folder? Parent { get; set; }
    public ICollection<Folder> SubFolders { get; set; } = [];
    public ICollection<FileItem> Files { get; set; } = [];
    public Guid CreatedByUserId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}
