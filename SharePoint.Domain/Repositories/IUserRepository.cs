using SharePoint.Domain.Entities;

namespace SharePoint.Application.Abstractions;

public interface IUserRepository
{
    Task<AppUser?> GetByAzureAdObjectIdAsync(string azureAdObjectId, CancellationToken cancellationToken);
    Task<AppUser> AddAsync(AppUser user, CancellationToken cancellationToken);
    Task<AppUser> UpdateAsync(AppUser user, CancellationToken cancellationToken);
}
