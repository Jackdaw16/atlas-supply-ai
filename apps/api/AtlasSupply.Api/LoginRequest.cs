namespace AtlasSupply.Api;

public sealed record LoginRequest(string? Username, string? Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string Username,
    IReadOnlyList<string> Scopes);
