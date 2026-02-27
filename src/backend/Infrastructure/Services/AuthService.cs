using CongNoGolden.Application.Auth;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CongNoGolden.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly ConGNoDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly AuthSecurityOptions _authSecurityOptions;

    public AuthService(
        ConGNoDbContext db,
        IJwtTokenService jwtTokenService,
        IOptions<JwtOptions> jwtOptions,
        IOptions<AuthSecurityOptions> authSecurityOptions)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _jwtOptions = jwtOptions.Value;
        _authSecurityOptions = authSecurityOptions.Value;
    }

    public async Task<AuthSession> LoginAsync(LoginRequest request, AuthRequestContext? requestContext, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername, ct);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var now = DateTimeOffset.UtcNow;
        if (IsUserLocked(user, now))
        {
            throw new UnauthorizedAccessException("Account is temporarily locked. Try again later.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await RegisterFailedLoginAsync(user, now, ct);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        ResetLoginFailureState(user);

        var roles = await LoadRoles(user.Id, ct);
        var access = _jwtTokenService.CreateToken(user.Id, user.Username, roles);
        var refresh = CreateRefreshTokenEntity(user.Id, requestContext: requestContext);
        _db.RefreshTokens.Add(refresh.Entity);
        await _db.SaveChangesAsync(ct);

        return new AuthSession(access, refresh.Token, refresh.Entity.ExpiresAt);
    }

    public async Task<AuthSession> RefreshAsync(string refreshToken, AuthRequestContext? requestContext, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token missing.");
        }

        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(refreshToken);

        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.RevokedAt == null, ct);

        if (existing is null || existing.ExpiresAt <= now)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }

        if (existing.AbsoluteExpiresAt <= now)
        {
            throw new UnauthorizedAccessException("Refresh token absolute lifetime exceeded.");
        }

        var binding = ResolveBinding(requestContext);
        if (IsContextMismatch(existing, binding.DeviceFingerprintHash, binding.IpPrefix))
        {
            throw new UnauthorizedAccessException("Refresh token context mismatch.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == existing.UserId, ct);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User is inactive.");
        }

        existing.RevokedAt = now;

        var roles = await LoadRoles(user.Id, ct);
        var access = _jwtTokenService.CreateToken(user.Id, user.Username, roles);
        var refreshed = CreateRefreshTokenEntity(
            user.Id,
            now,
            existing.AbsoluteExpiresAt,
            requestContext);
        _db.RefreshTokens.Add(refreshed.Entity);

        await _db.SaveChangesAsync(ct);

        return new AuthSession(access, refreshed.Token, refreshed.Entity.ExpiresAt);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = HashToken(refreshToken);
        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.RevokedAt == null, ct);

        if (existing is null)
        {
            return;
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<string>> LoadRoles(Guid userId, CancellationToken ct)
    {
        return await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Code)
            .ToListAsync(ct);
    }

    private (string Token, RefreshToken Entity) CreateRefreshTokenEntity(
        Guid userId,
        DateTimeOffset? now = null,
        DateTimeOffset? absoluteExpiresAt = null,
        AuthRequestContext? requestContext = null)
    {
        var issuedAt = now ?? DateTimeOffset.UtcNow;
        var token = CreateRefreshToken();
        var absoluteExpiry = absoluteExpiresAt ?? ResolveAbsoluteExpiry(issuedAt);
        var expiresAt = ResolveSlidingExpiry(issuedAt, absoluteExpiry);
        var binding = ResolveBinding(requestContext);
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(token),
            DeviceFingerprintHash = binding.DeviceFingerprintHash,
            IpPrefix = binding.IpPrefix,
            ExpiresAt = expiresAt,
            AbsoluteExpiresAt = absoluteExpiry,
            CreatedAt = issuedAt
        };

        return (token, entity);
    }

    private DateTimeOffset ResolveAbsoluteExpiry(DateTimeOffset issuedAt)
    {
        var refreshDays = Math.Max(_jwtOptions.RefreshTokenDays, 1);
        var absoluteDays = Math.Max(_jwtOptions.RefreshTokenAbsoluteDays, refreshDays);
        return issuedAt.AddDays(absoluteDays);
    }

    private DateTimeOffset ResolveSlidingExpiry(DateTimeOffset issuedAt, DateTimeOffset absoluteExpiry)
    {
        var refreshDays = Math.Max(_jwtOptions.RefreshTokenDays, 1);
        var slidingExpiry = issuedAt.AddDays(refreshDays);
        return slidingExpiry <= absoluteExpiry ? slidingExpiry : absoluteExpiry;
    }

    private static string CreateRefreshToken()
    {
        var buffer = new byte[64];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private bool IsUserLocked(User user, DateTimeOffset now)
    {
        if (!_authSecurityOptions.EnableLoginLockout)
        {
            return false;
        }

        if (!user.LockoutEndAt.HasValue)
        {
            return false;
        }

        if (user.LockoutEndAt.Value > now)
        {
            return true;
        }

        // Lockout already expired, reset counters so user can retry normally.
        user.LockoutEndAt = null;
        user.FailedLoginCount = 0;
        return false;
    }

    private async Task RegisterFailedLoginAsync(User user, DateTimeOffset now, CancellationToken ct)
    {
        user.LastFailedLoginAt = now;

        if (!_authSecurityOptions.EnableLoginLockout)
        {
            await _db.SaveChangesAsync(ct);
            return;
        }

        var maxAttempts = Math.Max(1, _authSecurityOptions.MaxFailedLoginAttempts);
        user.FailedLoginCount = Math.Max(0, user.FailedLoginCount) + 1;

        if (user.FailedLoginCount >= maxAttempts)
        {
            var lockoutMinutes = Math.Max(1, _authSecurityOptions.LockoutMinutes);
            user.LockoutEndAt = now.AddMinutes(lockoutMinutes);
            user.FailedLoginCount = 0;
            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Account is temporarily locked. Try again later.");
        }

        await _db.SaveChangesAsync(ct);
    }

    private static void ResetLoginFailureState(User user)
    {
        if (user.FailedLoginCount == 0 && user.LockoutEndAt is null && user.LastFailedLoginAt is null)
        {
            return;
        }

        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.LastFailedLoginAt = null;
    }

    private static (string? DeviceFingerprintHash, string? IpPrefix) ResolveBinding(AuthRequestContext? requestContext)
    {
        var deviceHash = HashDeviceFingerprint(requestContext?.UserAgent);
        var ipPrefix = ResolveIpPrefix(requestContext?.ClientIp);
        return (deviceHash, ipPrefix);
    }

    private static bool IsContextMismatch(
        RefreshToken token,
        string? deviceFingerprintHash,
        string? ipPrefix)
    {
        var deviceMismatch = !string.IsNullOrWhiteSpace(token.DeviceFingerprintHash)
            && !string.IsNullOrWhiteSpace(deviceFingerprintHash)
            && !string.Equals(token.DeviceFingerprintHash, deviceFingerprintHash, StringComparison.Ordinal);

        var ipMismatch = !string.IsNullOrWhiteSpace(token.IpPrefix)
            && !string.IsNullOrWhiteSpace(ipPrefix)
            && !string.Equals(token.IpPrefix, ipPrefix, StringComparison.OrdinalIgnoreCase);

        return deviceMismatch && ipMismatch;
    }

    private static string? HashDeviceFingerprint(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var normalized = userAgent.Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private static string? ResolveIpPrefix(string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            return null;
        }

        if (!IPAddress.TryParse(clientIp.Trim(), out var parsed))
        {
            return null;
        }

        var bytes = parsed.GetAddressBytes();
        if (bytes.Length == 4)
        {
            return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.0/24";
        }

        if (bytes.Length == 16)
        {
            return $"{bytes[0]:x2}{bytes[1]:x2}:{bytes[2]:x2}{bytes[3]:x2}:{bytes[4]:x2}{bytes[5]:x2}:{bytes[6]:x2}{bytes[7]:x2}::/64";
        }

        return null;
    }
}
