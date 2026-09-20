using System.Net.Http.Headers;
using Google.Apis.Auth.OAuth2;

namespace AtlasSupply.Infrastructure.Agents;

public interface IMcpIdTokenProvider
{
    Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken);
}

public sealed class GoogleMcpIdTokenProvider : IMcpIdTokenProvider
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private OidcToken? _oidcToken;
    private string? _audience;

    public async Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        var oidcToken = await GetOidcTokenAsync(audience, cancellationToken);
        return await oidcToken.GetAccessTokenAsync(cancellationToken);
    }

    private async Task<OidcToken> GetOidcTokenAsync(string audience, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_oidcToken is not null && string.Equals(_audience, audience, StringComparison.Ordinal))
            {
                return _oidcToken;
            }

            var credential = await GoogleCredential.GetApplicationDefaultAsync(cancellationToken);
            _oidcToken = await credential.GetOidcTokenAsync(
                OidcTokenOptions.FromTargetAudience(audience),
                cancellationToken);
            _audience = audience;
            return _oidcToken;
        }
        finally
        {
            _lock.Release();
        }
    }
}

public sealed class McpCloudRunAuthenticationHandler : DelegatingHandler
{
    private readonly string? _audience;
    private readonly IMcpIdTokenProvider _idTokenProvider;

    public McpCloudRunAuthenticationHandler(Uri endpoint, IMcpIdTokenProvider idTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(idTokenProvider);

        _audience = McpCloudRunAuthentication.GetAudience(endpoint);
        _idTokenProvider = idTokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_audience is not null)
        {
            var token = await _idTokenProvider.GetTokenAsync(_audience, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

public static class McpCloudRunAuthentication
{
    public static string? GetAudience(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            endpoint.IsLoopback ||
            string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return endpoint.GetLeftPart(UriPartial.Authority);
    }
}
