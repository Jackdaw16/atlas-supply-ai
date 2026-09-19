using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using AtlasSupply.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AtlasSupply.Infrastructure.Security;

public sealed class JwtAccessTokenIssuer(IConfiguration configuration, TimeProvider timeProvider) : IAccessTokenIssuer
{
    public Task<IssuedAccessToken> IssueAsync(AccessTokenIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.SubjectId == Guid.Empty)
        {
            throw new ArgumentException("Token subject is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("Token username is required.", nameof(request));
        }

        var settings = ResolveSettings();
        var issuedAtUtc = timeProvider.GetUtcNow();
        var expiresAtUtc = issuedAtUtc.AddMinutes(settings.AccessTokenMinutes);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, request.SubjectId.ToString("D")),
            new Claim("name", request.Username),
            new Claim("preferred_username", request.Username),
            new Claim("scope", string.Join(' ', request.Scopes)),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                issuedAtUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: issuedAtUtc.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return Task.FromResult(new IssuedAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc));
    }

    private JwtAccessTokenSettings ResolveSettings()
    {
        var issuer = RequireSetting("Jwt:Issuer");
        var audience = RequireSetting("Jwt:Audience");
        var signingKey = RequireSetting("JWT_SIGNING_KEY");
        var accessTokenMinutesValue = RequireSetting("Jwt:AccessTokenMinutes");

        if (!int.TryParse(accessTokenMinutesValue, out var accessTokenMinutes) || accessTokenMinutes < 1)
        {
            throw new InvalidOperationException("Jwt:AccessTokenMinutes must be a positive integer.");
        }

        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("JWT_SIGNING_KEY must contain at least 32 UTF-8 bytes.");
        }

        return new JwtAccessTokenSettings(issuer, audience, signingKey, accessTokenMinutes);
    }

    private string RequireSetting(string key)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required for JWT access token issuance.");
        }

        return value;
    }

    private sealed record JwtAccessTokenSettings(
        string Issuer,
        string Audience,
        string SigningKey,
        int AccessTokenMinutes);
}
