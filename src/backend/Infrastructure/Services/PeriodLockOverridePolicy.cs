using CongNoGolden.Application.Common.Interfaces;

namespace CongNoGolden.Infrastructure.Services;

public static class PeriodLockOverridePolicy
{
    public static string RequireOverride(ICurrentUser currentUser, string? reason)
    {
        if (!IsAdminOrSupervisor(currentUser))
        {
            throw new UnauthorizedAccessException("Period lock override requires Admin or Supervisor.");
        }

        var trimmed = (reason ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Override reason is required.");
        }

        return trimmed;
    }

    public static bool IsAdminOrSupervisor(ICurrentUser currentUser)
    {
        return currentUser.Roles.Any(role =>
            role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Supervisor", StringComparison.OrdinalIgnoreCase));
    }
}
