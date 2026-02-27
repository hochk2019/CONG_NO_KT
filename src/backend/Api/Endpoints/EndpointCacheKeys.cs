using System.Security.Claims;

namespace CongNoGolden.Api.Endpoints;

internal static class EndpointCacheKeys
{
    public static string ForHttpRequest(HttpContext context)
    {
        var userId =
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? "anonymous";

        var roles = context.User
            .FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        var roleKey = string.Join(",", roles);
        var query = context.Request.QueryString.HasValue
            ? context.Request.QueryString.Value
            : string.Empty;

        return $"{context.Request.Path}|{query}|uid={userId}|roles={roleKey}";
    }
}
