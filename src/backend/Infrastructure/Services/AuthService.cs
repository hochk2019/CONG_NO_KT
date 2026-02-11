using CongNoGolden.Application.Auth;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace CongNoGolden.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly ConGNoDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;

    public AuthService(ConGNoDbContext db, IJwtTokenService jwtTokenService, IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => EF.Functions.ILike(u.Username, request.Username), ct);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var roles = await LoadRoles(user.Id, ct);
        var access = _jwtTokenService.CreateToken(user.Id, user.Username, roles);
        var refresh = CreateRefreshTokenEntity(user.Id);
        _db.RefreshTokens.Add(refresh.Entity);
        await _db.SaveChangesAsync(ct);

        return new AuthSession(access, refresh.Token, refresh.Entity.ExpiresAt);
    }

    public async Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken ct)
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

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == existing.UserId, ct);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User is inactive.");
        }

        existing.RevokedAt = now;

        var roles = await LoadRoles(user.Id, ct);
        var access = _jwtTokenService.CreateToken(user.Id, user.Username, roles);
        var refreshed = CreateRefreshTokenEntity(user.Id, now);
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

    private (string Token, RefreshToken Entity) CreateRefreshTokenEntity(Guid userId, DateTimeOffset? now = null)
    {
        var issuedAt = now ?? DateTimeOffset.UtcNow;
        var token = CreateRefreshToken();
        var expiresAt = issuedAt.AddDays(Math.Max(_jwtOptions.RefreshTokenDays, 1));
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(token),
            ExpiresAt = expiresAt,
            CreatedAt = issuedAt
        };

        return (token, entity);
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
}
