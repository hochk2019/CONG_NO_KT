using CongNoGolden.Infrastructure.Data.Entities;
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

        foreach (var role in DefaultRoles)
        {
            var exists = await db.Roles.AnyAsync(r => r.Code == role.Code, ct);
            if (!exists)
            {
                db.Roles.Add(new Role { Code = role.Code, Name = role.Name });
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
