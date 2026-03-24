using System.Security.Claims;
using SharePoint.Application.Abstractions;
using SharePoint.Domain.Entities;

namespace SharePoint.Application.Services;

public class AzureAdUserSyncService : IAzureAdUserSyncService
{
    private readonly IUserRepository _userRepository;

    public AzureAdUserSyncService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AppUser> EnsureUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var objectId = principal.FindFirst("oid")?.Value
            ?? throw new UnauthorizedAccessException("Missing oid claim.");

        var tenantId = principal.FindFirst("tid")?.Value
            ?? string.Empty;

        var email = principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value
            ?? "unknown@local";

        var displayName = principal.Identity?.Name
            ?? principal.FindFirst("name")?.Value
            ?? email;

        var existing = await _userRepository.GetByAzureAdObjectIdAsync(objectId, cancellationToken);

        if (existing is not null)
        {
            existing.LastLoginAtUtc = DateTime.UtcNow;
            existing.Email = email;
            existing.DisplayName = displayName;
            existing.TenantId = tenantId;
            existing.ModifiedAtUtc = DateTime.UtcNow;

            await _userRepository.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        var user = new AppUser
        {
            AzureAdObjectId = objectId,
            TenantId = tenantId,
            Email = email,
            DisplayName = displayName,
            CreatedAtUtc = DateTime.UtcNow,
            LastLoginAtUtc = DateTime.UtcNow
        };

        return await _userRepository.AddAsync(user, cancellationToken);
    }
}
