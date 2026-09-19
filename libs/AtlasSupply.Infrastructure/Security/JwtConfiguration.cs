using System.Text;
using Microsoft.Extensions.Configuration;

namespace AtlasSupply.Infrastructure.Security;

public static class JwtConfiguration
{
    public static JwtValidationSettings ResolveValidationSettings(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var issuer = RequireSetting(configuration, "Jwt:Issuer");
        var audience = RequireSetting(configuration, "Jwt:Audience");
        var signingKey = RequireSetting(configuration, "JWT_SIGNING_KEY");

        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("JWT_SIGNING_KEY must contain at least 32 UTF-8 bytes.");
        }

        return new JwtValidationSettings(issuer, audience, signingKey);
    }

    public static JwtAccessTokenSettings ResolveAccessTokenSettings(IConfiguration configuration)
    {
        var validation = ResolveValidationSettings(configuration);
        var accessTokenMinutesValue = RequireSetting(configuration, "Jwt:AccessTokenMinutes");

        if (!int.TryParse(accessTokenMinutesValue, out var accessTokenMinutes) || accessTokenMinutes < 1)
        {
            throw new InvalidOperationException("Jwt:AccessTokenMinutes must be a positive integer.");
        }

        return new JwtAccessTokenSettings(
            validation.Issuer,
            validation.Audience,
            validation.SigningKey,
            accessTokenMinutes);
    }

    private static string RequireSetting(IConfiguration configuration, string key)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required for JWT configuration.");
        }

        return value;
    }
}

public sealed record JwtValidationSettings(string Issuer, string Audience, string SigningKey);

public sealed record JwtAccessTokenSettings(
    string Issuer,
    string Audience,
    string SigningKey,
    int AccessTokenMinutes);
