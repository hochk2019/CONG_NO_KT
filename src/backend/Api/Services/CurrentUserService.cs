using System.Security.Claims;
using CongNoGolden.Application.Common.Interfaces;

namespace CongNoGolden.Api.Services;

public sealed class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId => GetUserId(_httpContextAccessor.HttpContext?.User);

    public string? Username => _httpContextAccessor.HttpContext?.User?.Identity?.Name;

    public IReadOnlyList<string> Roles => _httpContextAccessor.HttpContext?.User
        ?.FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .ToArray() ?? Array.Empty<string>();

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private static Guid? GetUserId(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(idValue, out var id) ? id : null;
    }
}
