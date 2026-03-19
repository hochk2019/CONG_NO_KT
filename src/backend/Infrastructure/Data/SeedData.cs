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
        (AppPermissions.CustomerView, "Xem khách hàng"),
        (AppPermissions.CustomerEditAll, "Sửa mọi khách hàng"),
        (AppPermissions.CustomerEditOwned, "Sửa khách hàng phụ trách"),
        (AppPermissions.CustomerEditUnassigned, "Sửa khách hàng chưa phân công"),
        (AppPermissions.CustomerAssignmentManage, "Quản lý phân công khách hàng"),
        (AppPermissions.ImportUpload, "Tải tệp nhập liệu"),
        (AppPermissions.ImportHistory, "Xem lịch sử nhập liệu"),
        (AppPermissions.ImportCommitInvoice, "Ghi nhận nhập hóa đơn"),
        (AppPermissions.ImportCommitAdvance, "Ghi nhận nhập trả hộ"),
        (AppPermissions.ImportCommitReceipt, "Ghi nhận nhập thu tiền"),
        (AppPermissions.ImportRollback, "Hoàn tác đợt nhập"),
        (AppPermissions.AdvanceManage, "Quản lý trả hộ"),
        (AppPermissions.ReceiptApprove, "Duyệt thu tiền"),
        (AppPermissions.PeriodLockManage, "Quản lý khóa kỳ"),
        (AppPermissions.ReportsView, "Xem báo cáo"),
        (AppPermissions.InvoiceManage, "Quản lý hóa đơn"),
        (AppPermissions.AuditView, "Xem nhật ký hệ thống"),
        (AppPermissions.AdminHealthView, "Xem sức khỏe hệ thống"),
        (AppPermissions.RiskView, "Xem cảnh báo rủi ro"),
        (AppPermissions.RiskManage, "Quản lý cảnh báo rủi ro"),
        (AppPermissions.BackupManage, "Quản lý sao lưu"),
        (AppPermissions.BackupRestore, "Khôi phục bản sao lưu"),
        (AppPermissions.AdminManage, "Quản trị hệ thống")
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
                AppPermissions.ReceiptApprove,
                AppPermissions.PeriodLockManage,
                AppPermissions.ReportsView,
                AppPermissions.InvoiceManage,
                AppPermissions.AuditView,
                AppPermissions.AdminHealthView,
                AppPermissions.RiskView,
                AppPermissions.RiskManage,
                AppPermissions.BackupManage
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
                AppPermissions.ReceiptApprove,
                AppPermissions.ReportsView,
                AppPermissions.RiskView
            ],
            ["Viewer"] =
            [
                AppPermissions.CustomerView,
                AppPermissions.ReportsView,
                AppPermissions.RiskView
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
            var existingPermission = await db.Permissions.FirstOrDefaultAsync(p => p.Code == permission.Code, ct);
            if (existingPermission is null)
            {
                db.Permissions.Add(new Permission { Code = permission.Code, Name = permission.Name });
                continue;
            }

            if (!string.Equals(existingPermission.Name, permission.Name, StringComparison.Ordinal))
            {
                existingPermission.Name = permission.Name;
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

