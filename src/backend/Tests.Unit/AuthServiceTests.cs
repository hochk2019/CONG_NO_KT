using System.Security.Cryptography;
using System.Text;
using CongNoGolden.Application.Auth;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests.Unit;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_LocksAccountAfterConfiguredFailures()
    {
        await using var db = CreateDbContext(nameof(LoginAsync_LocksAccountAfterConfiguredFailures));
        await SeedUserAsync(db);

        var service = CreateService(db, new AuthSecurityOptions
        {
            EnableLoginLockout = true,
            MaxFailedLoginAttempts = 3,
            LockoutMinutes = 30
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(new LoginRequest("tester", "wrong-pass"), null, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(new LoginRequest("tester", "wrong-pass"), null, CancellationToken.None));

        var lockoutEx = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(new LoginRequest("tester", "wrong-pass"), null, CancellationToken.None));
        Assert.Contains("temporarily locked", lockoutEx.Message, StringComparison.OrdinalIgnoreCase);

        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "tester");
        Assert.NotNull(user.LockoutEndAt);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public async Task LoginAsync_ResetsFailureStateAfterSuccessfulLogin()
    {
        await using var db = CreateDbContext(nameof(LoginAsync_ResetsFailureStateAfterSuccessfulLogin));
        var user = await SeedUserAsync(db);

        user.FailedLoginCount = 2;
        user.LastFailedLoginAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        user.LockoutEndAt = null;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var session = await service.LoginAsync(new LoginRequest("tester", "StrongPass123"), null, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(session.Access.AccessToken));

        var reloaded = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "tester");
        Assert.Equal(0, reloaded.FailedLoginCount);
        Assert.Null(reloaded.LastFailedLoginAt);
        Assert.Null(reloaded.LockoutEndAt);
    }

    [Fact]
    public async Task LoginAsync_RejectsWhileLockoutStillActive()
    {
        await using var db = CreateDbContext(nameof(LoginAsync_RejectsWhileLockoutStillActive));
        var user = await SeedUserAsync(db);

        user.LockoutEndAt = DateTimeOffset.UtcNow.AddMinutes(10);
        user.FailedLoginCount = 0;
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(new LoginRequest("tester", "StrongPass123"), null, CancellationToken.None));
        Assert.Contains("temporarily locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_RejectsTokenBeyondAbsoluteExpiry()
    {
        await using var db = CreateDbContext(nameof(RefreshAsync_RejectsTokenBeyondAbsoluteExpiry));
        var now = DateTimeOffset.UtcNow;
        var user = await SeedUserAsync(db);
        var token = "refresh-expired-absolute";

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(token),
            CreatedAt = now.AddDays(-31),
            ExpiresAt = now.AddHours(1),
            AbsoluteExpiresAt = now.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RefreshAsync(token, CreateRequestContext("10.10.10.15", "ua-1"), CancellationToken.None));

        Assert.Contains("absolute lifetime", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_PreservesAbsoluteExpiryAcrossRotation()
    {
        await using var db = CreateDbContext(nameof(RefreshAsync_PreservesAbsoluteExpiryAcrossRotation));
        var now = DateTimeOffset.UtcNow;
        var user = await SeedUserAsync(db);
        var token = "refresh-rotate";
        var absoluteExpiresAt = now.AddDays(10);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(token),
            CreatedAt = now.AddDays(-3),
            ExpiresAt = now.AddDays(2),
            AbsoluteExpiresAt = absoluteExpiresAt
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var session = await service.RefreshAsync(token, CreateRequestContext("10.10.10.15", "ua-1"), CancellationToken.None);

        Assert.True(session.RefreshExpiresAt <= absoluteExpiresAt);

        var tokens = await db.RefreshTokens
            .AsNoTracking()
            .Where(rt => rt.UserId == user.Id)
            .OrderBy(rt => rt.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.NotNull(tokens[0].RevokedAt);
        Assert.Null(tokens[1].RevokedAt);
        Assert.Equal(absoluteExpiresAt, tokens[1].AbsoluteExpiresAt);
        Assert.True(tokens[1].ExpiresAt <= tokens[1].AbsoluteExpiresAt);
    }

    [Fact]
    public async Task RefreshAsync_Rejects_WhenIpAndDeviceBothMismatch()
    {
        await using var db = CreateDbContext(nameof(RefreshAsync_Rejects_WhenIpAndDeviceBothMismatch));
        var now = DateTimeOffset.UtcNow;
        var user = await SeedUserAsync(db);
        var token = "refresh-binding-reject";
        var originalContext = CreateRequestContext("10.10.10.15", "ua-1");
        var requestContext = CreateRequestContext("172.30.40.55", "ua-2");

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(token),
            CreatedAt = now.AddDays(-1),
            ExpiresAt = now.AddDays(1),
            AbsoluteExpiresAt = now.AddDays(10),
            DeviceFingerprintHash = HashDeviceFingerprint(originalContext.UserAgent),
            IpPrefix = ResolveIpPrefix(originalContext.ClientIp)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RefreshAsync(token, requestContext, CancellationToken.None));

        Assert.Contains("context mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_Allows_WhenOnlyIpChangesButDeviceMatches()
    {
        await using var db = CreateDbContext(nameof(RefreshAsync_Allows_WhenOnlyIpChangesButDeviceMatches));
        var now = DateTimeOffset.UtcNow;
        var user = await SeedUserAsync(db);
        var token = "refresh-binding-allow";
        var originalContext = CreateRequestContext("10.10.10.15", "ua-1");
        var requestContext = CreateRequestContext("172.30.40.55", "ua-1");

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(token),
            CreatedAt = now.AddDays(-1),
            ExpiresAt = now.AddDays(1),
            AbsoluteExpiresAt = now.AddDays(10),
            DeviceFingerprintHash = HashDeviceFingerprint(originalContext.UserAgent),
            IpPrefix = ResolveIpPrefix(originalContext.ClientIp)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var session = await service.RefreshAsync(token, requestContext, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(session.Access.AccessToken));
    }

    private static AuthService CreateService(
        ConGNoDbContext db,
        AuthSecurityOptions? authSecurityOptions = null)
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            Secret = "test-secret-with-at-least-32-characters",
            Issuer = "test",
            Audience = "test",
            RefreshTokenDays = 14,
            RefreshTokenAbsoluteDays = 30
        });

        var securityOptions = Options.Create(authSecurityOptions ?? new AuthSecurityOptions());
        return new AuthService(db, new FakeJwtTokenService(), jwtOptions, securityOptions);
    }

    private static async Task<User> SeedUserAsync(ConGNoDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "tester",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPass123"),
            IsActive = true,
            FailedLoginCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static ConGNoDbContext CreateDbContext(string testName)
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"auth-service-{testName}")
            .Options;
        return new ConGNoDbContext(options);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static AuthRequestContext CreateRequestContext(string? ip, string? userAgent)
    {
        return new AuthRequestContext(ip, userAgent);
    }

    private static string HashDeviceFingerprint(string? userAgent)
    {
        var normalized = (userAgent ?? string.Empty).Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private static string? ResolveIpPrefix(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return null;
        }

        if (System.Net.IPAddress.TryParse(ip.Trim(), out var parsed))
        {
            var bytes = parsed.GetAddressBytes();
            if (bytes.Length == 4)
            {
                return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.0/24";
            }

            if (bytes.Length == 16)
            {
                return $"{bytes[0]:x2}{bytes[1]:x2}:{bytes[2]:x2}{bytes[3]:x2}:{bytes[4]:x2}{bytes[5]:x2}:{bytes[6]:x2}{bytes[7]:x2}::/64";
            }
        }

        return null;
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public LoginResult CreateToken(Guid userId, string username, IReadOnlyList<string> roles)
        {
            return new LoginResult("access-token", DateTimeOffset.UtcNow.AddMinutes(60));
        }
    }
}
