namespace CongNoGolden.Application.Auth;

public interface IAuthService
{
    Task<AuthSession> LoginAsync(LoginRequest request, AuthRequestContext? requestContext, CancellationToken ct);
    Task<AuthSession> RefreshAsync(string refreshToken, AuthRequestContext? requestContext, CancellationToken ct);
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}
