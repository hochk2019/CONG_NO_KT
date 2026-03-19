using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Services.Common;

namespace CongNoGolden.Infrastructure.Security;

public static class ImportPermissionEvaluator
{
    public static bool CanCommitImport(ICurrentUser currentUser, string? importType)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        var permission = ResolveCommitPermission(importType);
        return permission is not null && currentUser.HasAnyPermission(permission);
    }

    public static bool CanRollbackImport(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        return currentUser.HasAnyPermission(AppPermissions.ImportRollback);
    }

    public static string? ResolveCommitPermission(string? importType)
    {
        return (importType ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "INVOICE" => AppPermissions.ImportCommitInvoice,
            "ADVANCE" => AppPermissions.ImportCommitAdvance,
            "RECEIPT" => AppPermissions.ImportCommitReceipt,
            _ => null
        };
    }
}
