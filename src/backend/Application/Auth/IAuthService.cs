namespace CongNoGolden.Application.Auth;

public interface IAuthService
{
    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken ct);
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}
