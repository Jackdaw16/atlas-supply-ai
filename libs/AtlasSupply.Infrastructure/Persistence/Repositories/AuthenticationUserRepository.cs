using AtlasSupply.Application;
using AtlasSupply.Domain;
using Microsoft.EntityFrameworkCore;

namespace AtlasSupply.Infrastructure.Persistence.Repositories;

internal sealed class AuthenticationUserRepository(AtlasSupplyDbContext dbContext) : IUserCredentialStore
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalizedUsername = username.Trim().ToUpperInvariant();

        return dbContext.Users
            .Include(static user => user.ScopeAssignments)
            .SingleOrDefaultAsync(
                user => user.NormalizedUsername == normalizedUsername,
                cancellationToken);
    }
}
