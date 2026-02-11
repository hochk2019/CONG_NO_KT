namespace CongNoGolden.Application.Auth;

public interface IJwtTokenService
{
    LoginResult CreateToken(Guid userId, string username, IReadOnlyList<string> roles);
}
