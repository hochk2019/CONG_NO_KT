using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CongNoGolden.Infrastructure.Data;

public static class SeedData
{
    private static readonly (string Code, string Name)[] DefaultRoles =
    [
        ("Admin", "Admin"),
        ("Supervisor", "Supervisor"),
        ("Accountant", "Accountant"),
        ("Viewer", "Viewer")
    ];

    private static readonly (string Code, string Name)[] DefaultPermissions =
    [
        (AppPermissions.CustomerView, AppPermissions.CustomerView),
        (AppPermissions.CustomerEditAll, AppPermissions.CustomerEditAll),
        (AppPermissions.CustomerEditOwned, AppPermissions.CustomerEditOwned),
        (AppPermissions.CustomerEditUnassigned, AppPermissions.CustomerEditUnassigned),
        (AppPermissions.CustomerAssignmentManage, AppPermissions.CustomerAssignmentManage),
        (AppPermissions.ImportUpload, AppPermissions.ImportUpload),
        (AppPermissions.ImportHistory, AppPermissions.ImportHistory),
        (AppPermissions.ImportCommitInvoice, AppPermissions.ImportCommitInvoice),
        (AppPermissions.ImportCommitAdvance, AppPermissions.ImportCommitAdvance),
        (AppPermissions.ImportCommitReceipt, AppPermissions.ImportCommitReceipt),
        (AppPermissions.ImportRollback, AppPermissions.ImportRollback),
        (AppPermissions.AdvanceManage, AppPermissions.AdvanceManage),
        (AppPermissions.ReceiptApprove, AppPermissions.ReceiptApprove),
        (AppPermissions.AdminManage, AppPermissions.AdminManage)
    ];

    private static readonly IReadOnlyDictionary<string, string[]> DefaultRolePermissions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] =
            [
                .. DefaultPermissions.Select(static permission => permission.Code)
            ],
            ["Supervisor"] =
            [
                AppPermissions.CustomerView,
                AppPermissions.CustomerEditAll,
                AppPermissions.CustomerAssignmentManage,
                AppPermissions.ImportUpload,
                AppPermissions.ImportHistory,
                AppPermissions.ImportCommitInvoice,
                AppPermissions.ImportCommitAdvance,
                AppPermissions.ImportCommitReceipt,
                AppPermissions.ImportRollback,
                AppPermissions.AdvanceManage,
                AppPermissions.ReceiptApprove
            ],
            ["Accountant"] =
            [
                AppPermissions.CustomerView,
                AppPermissions.CustomerEditOwned,
                AppPermissions.CustomerEditUnassigned,
                AppPermissions.ImportUpload,
                AppPermissions.ImportHistory,
                AppPermissions.ImportCommitInvoice,
                AppPermissions.ImportCommitAdvance,
                AppPermissions.AdvanceManage,
                AppPermissions.ReceiptApprove
            ],
            ["Viewer"] =
            [
                AppPermissions.CustomerView
            ]
        };

    public static async Task SeedAsync(ConGNoDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        var adminUsername = configuration["Seed:AdminUsername"];
        var adminPassword = configuration["Seed:AdminPassword"];
        var adminFullName = configuration["Seed:AdminFullName"];
        var adminEmail = configuration["Seed:AdminEmail"];
        var adminReset = bool.TryParse(configuration["Seed:AdminReset"], out var resetFlag) && resetFlag;

        if (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var roleCodes = DefaultRolePermissions.Keys.ToArray();
        var knownPermissionCodes = DefaultPermissions.Select(static item => item.Code).ToArray();

        foreach (var role in DefaultRoles)
        {
            var exists = await db.Roles.AnyAsync(r => r.Code == role.Code, ct);
            if (!exists)
            {
                db.Roles.Add(new Role { Code = role.Code, Name = role.Name });
            }
        }

        foreach (var permission in DefaultPermissions)
        {
            var exists = await db.Permissions.AnyAsync(p => p.Code == permission.Code, ct);
            if (!exists)
            {
                db.Permissions.Add(new Permission { Code = permission.Code, Name = permission.Name });
            }
        }

        await db.SaveChangesAsync(ct);

        var roleIds = await db.Roles
            .Where(role => roleCodes.Contains(role.Code))
            .ToDictionaryAsync(role => role.Code, role => role.Id, ct);
        var permissionIds = await db.Permissions
            .Where(permission => knownPermissionCodes.Contains(permission.Code))
            .ToDictionaryAsync(permission => permission.Code, permission => permission.Id, ct);
        var existingRolePermissions = await db.RolePermissions
            .AsNoTracking()
            .Select(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId })
            .ToListAsync(ct);
        var existingPairs = existingRolePermissions
            .Select(static rolePermission => (rolePermission.RoleId, rolePermission.PermissionId))
            .ToHashSet();

        foreach (var (roleCode, permissionCodes) in DefaultRolePermissions)
        {
            if (!roleIds.TryGetValue(roleCode, out var roleId))
            {
                continue;
            }

            foreach (var permissionCode in permissionCodes)
            {
                if (!permissionIds.TryGetValue(permissionCode, out var permissionId)
                    || existingPairs.Contains((roleId, permissionId)))
                {
                    continue;
                }

                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var user = await db.Users.FirstOrDefaultAsync(u => EF.Functions.ILike(u.Username, adminUsername), ct);
        var isNewUser = false;
        var needsUpdate = false;
        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Username = adminUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                FullName = string.IsNullOrWhiteSpace(adminFullName) ? null : adminFullName,
                Email = string.IsNullOrWhiteSpace(adminEmail) ? null : adminEmail,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Version = 0
            };

            db.Users.Add(user);
            isNewUser = true;
        }
        else if (adminReset)
        {
            if (!BCrypt.Net.BCrypt.Verify(adminPassword, user.PasswordHash))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
                needsUpdate = true;
            }

            if (!string.IsNullOrWhiteSpace(adminFullName) && user.FullName != adminFullName)
            {
                user.FullName = adminFullName;
                needsUpdate = true;
            }

            if (!string.IsNullOrWhiteSpace(adminEmail) && user.Email != adminEmail)
            {
                user.Email = adminEmail;
                needsUpdate = true;
            }

            if (!user.IsActive)
            {
                user.IsActive = true;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                user.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        if (isNewUser || needsUpdate)
        {
            await db.SaveChangesAsync(ct);
        }

        var adminRoleId = await db.Roles
            .Where(r => r.Code == "Admin")
            .Select(r => r.Id)
            .FirstAsync(ct);

        var hasAdminRole = await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == adminRoleId, ct);
        if (!hasAdminRole)
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRoleId });
        }

        await db.SaveChangesAsync(ct);
    }
}
