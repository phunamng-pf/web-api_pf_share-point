using System.Security.Claims;
using SharePoint.Application.Abstractions;
using SharePoint.Domain.Entities;

namespace SharePoint.Application.Services;

public class AzureAdUserSyncService : IAzureAdUserSyncService
{
    private static readonly TimeSpan LastLoginUpdateInterval = TimeSpan.FromMinutes(15);

    private readonly IUserRepository _userRepository;

    public AzureAdUserSyncService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AppUser> EnsureUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {

        var objectId = principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? throw new UnauthorizedAccessException("Missing oid claim.");

        var tenantId = principal.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value
            ?? string.Empty;

        var email = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? "unknown@local";

        var displayName = principal.FindFirst("name")?.Value
            ?? email;

        // Use auth_time claim if present for LastLoginAt
        var authTime = principal.FindFirst("auth_time")?.Value;
        DateTime lastLogin = DateTime.UtcNow;
        if (long.TryParse(authTime, out var seconds))
        {
            lastLogin = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        }

        var existing = await _userRepository.GetByAzureAdObjectIdAsync(objectId, cancellationToken);

        if (existing is not null)
        {
            bool changed = false;
            if (!string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                existing.Email = email;
                changed = true;
            }
            if (!string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal))
            {
                existing.DisplayName = displayName;
                changed = true;
            }
            if (!string.Equals(existing.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                existing.TenantId = tenantId;
                changed = true;
            }
            if (changed || existing.LastLoginAt != lastLogin)
            {
                existing.LastLoginAt = lastLogin;
                existing.ModifiedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(existing, cancellationToken);
            }
            return existing;
        }

        var user = new AppUser
        {
            AzureAdObjectId = objectId,
            TenantId = tenantId,
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        return await _userRepository.AddAsync(user, cancellationToken);
    }
}
