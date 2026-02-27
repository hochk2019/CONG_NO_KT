namespace CongNoGolden.Application.Auth;

public sealed record AuthRequestContext(
    string? ClientIp,
    string? UserAgent);
