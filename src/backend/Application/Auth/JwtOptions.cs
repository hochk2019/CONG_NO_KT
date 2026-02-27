namespace CongNoGolden.Application.Auth;

public sealed class JwtOptions
{
    public const string SecretEnvironmentVariable = "Jwt__Secret";
    public const string SecretPlaceholder = "CHANGE_ME_SUPER_SECRET_32_CHARS!";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
    public int RefreshTokenAbsoluteDays { get; set; } = 90;
    public string RefreshCookieName { get; set; } = "congno_refresh";
    public bool RefreshCookieSecure { get; set; } = false;
    public string RefreshCookieSameSite { get; set; } = "Strict";
    public string RefreshCookiePath { get; set; } = "/auth";
}
