using Microsoft.EntityFrameworkCore;
using SharePoint.Application.Abstractions;
using SharePoint.Domain.Entities;

namespace SharePoint.Infrastructure.Persistence;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AppUser?> GetByAzureAdObjectIdAsync(string azureAdObjectId, CancellationToken cancellationToken)
    {
        return _dbContext.Users
            .FirstOrDefaultAsync(x => x.AzureAdObjectId == azureAdObjectId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _dbContext.Users
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
    }

    public async Task<AppUser> AddAsync(AppUser user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<AppUser> UpdateAsync(AppUser user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }
}
