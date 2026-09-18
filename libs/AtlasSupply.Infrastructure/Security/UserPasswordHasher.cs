using AtlasSupply.Domain;
using Microsoft.AspNetCore.Identity;

namespace AtlasSupply.Infrastructure.Security;

internal sealed class UserPasswordHasher : IPasswordHasher<User>
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public string HashPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return _passwordHasher.HashPassword(user, password);
    }

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashedPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(providedPassword);

        return _passwordHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}
