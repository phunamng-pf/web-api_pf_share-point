using SharePoint.Domain.Entities;
using System.Security.Claims;

namespace SharePoint.Application.Abstractions;

public interface IAzureAdUserSyncService
{
    Task<AppUser> EnsureUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
