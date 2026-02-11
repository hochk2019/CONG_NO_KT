namespace CongNoGolden.Application.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
    public string RefreshCookieName { get; set; } = "congno_refresh";
    public bool RefreshCookieSecure { get; set; } = false;
}
