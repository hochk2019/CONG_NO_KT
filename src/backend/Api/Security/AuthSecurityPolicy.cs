using CongNoGolden.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace CongNoGolden.Api.Security;

public static class AuthSecurityPolicy
{
    public const string LoginRateLimiterPolicy = "auth-login";
    public const string RefreshRateLimiterPolicy = "auth-refresh";

    private const int MinJwtSecretLength = 32;
    private const int MinPasswordLength = 8;

    public static void ValidateJwtOptions(JwtOptions options, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(options);

        var secret = (options.Secret ?? string.Empty).Trim();
        if (secret.Length < MinJwtSecretLength)
        {
            throw new InvalidOperationException(
                $"JWT secret must be at least {MinJwtSecretLength} characters. Configure {JwtOptions.SecretEnvironmentVariable} from environment.");
        }

        if (!isDevelopment && string.Equals(secret, JwtOptions.SecretPlaceholder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SecretEnvironmentVariable} is still using the default placeholder. Configure a production secret via environment or secret manager.");
        }
    }

    public static string ResolveClientPartitionKey(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;
    }

    public static void ValidatePasswordComplexity(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Password is required.");
        }

        if (password.Length < MinPasswordLength)
        {
            throw new InvalidOperationException($"Password must be at least {MinPasswordLength} characters.");
        }

        if (!password.Any(char.IsUpper))
        {
            throw new InvalidOperationException("Password must include at least one uppercase letter.");
        }

        if (!password.Any(char.IsLower))
        {
            throw new InvalidOperationException("Password must include at least one lowercase letter.");
        }

        if (!password.Any(char.IsDigit))
        {
            throw new InvalidOperationException("Password must include at least one number.");
        }
    }

    public static SameSiteMode ResolveSameSiteMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SameSiteMode.Strict;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "LAX" => SameSiteMode.Lax,
            "NONE" => SameSiteMode.None,
            "UNSPECIFIED" => SameSiteMode.Unspecified,
            _ => SameSiteMode.Strict
        };
    }

    public static string ResolveCookiePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/auth";
        }

        var path = value.Trim();
        return path.StartsWith('/') ? path : $"/{path}";
    }
}
