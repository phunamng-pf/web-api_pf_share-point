using SharePoint.Domain.Enums;

namespace SharePoint.Domain.Entities;

public sealed class ItemPermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public Guid ItemId { get; set; }
    public ItemType ItemType { get; set; }
    public PermissionRole Role { get; set; }
}
