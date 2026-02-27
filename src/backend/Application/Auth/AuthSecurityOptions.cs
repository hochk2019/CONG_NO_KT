namespace CongNoGolden.Application.Auth;

public sealed class AuthSecurityOptions
{
    public bool EnableLoginLockout { get; set; } = true;
    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
