using CongNoGolden.Application.Common.Interfaces;

namespace CongNoGolden.Infrastructure.Services.Common;

public static class CurrentUserAccessExtensions
{
    private static readonly string[] DefaultPrivilegedRoles = ["Admin", "Supervisor", "Viewer"];

    public static Guid EnsureUser(this ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        if (currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("User context missing.");
        }

        return currentUser.UserId.Value;
    }

    public static bool HasAnyRole(this ICurrentUser currentUser, params string[] roles)
    {
        return HasAnyRole(currentUser, (IEnumerable<string>)roles);
    }

    public static bool HasAnyRole(this ICurrentUser currentUser, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(roles);

        foreach (var expectedRole in roles)
        {
            if (string.IsNullOrWhiteSpace(expectedRole))
            {
                continue;
            }

            foreach (var role in currentUser.Roles)
            {
                if (string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static Guid? ResolveOwnerFilter(
        this ICurrentUser currentUser,
        Guid? explicitOwner = null,
        IEnumerable<string>? privilegedRoles = null)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        var roles = privilegedRoles ?? DefaultPrivilegedRoles;
        if (currentUser.HasAnyRole(roles))
        {
            return explicitOwner;
        }

        return currentUser.EnsureUser();
    }
}
