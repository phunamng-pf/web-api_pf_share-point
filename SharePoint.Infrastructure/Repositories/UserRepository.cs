using Microsoft.EntityFrameworkCore;
using SharePoint.Application.Abstractions;
using SharePoint.Domain.Entities;

namespace SharePoint.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
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
