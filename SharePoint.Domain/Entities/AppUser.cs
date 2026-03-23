using SharePoint.Domain.Common;

namespace SharePoint.Domain.Entities;

public sealed class AppUser : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AzureAdObjectId { get; set; }
    public required string TenantId { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public DateTime LastLoginAtUtc { get; set; } = DateTime.UtcNow;
}
