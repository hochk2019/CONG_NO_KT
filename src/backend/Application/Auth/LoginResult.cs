namespace CongNoGolden.Application.Auth;

public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset ExpiresAt
);
