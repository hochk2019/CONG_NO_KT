namespace CongNoGolden.Application.Auth;

public sealed record AuthSession(
    LoginResult Access,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt
);
