using CongNoGolden.Api;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/users", async (
            string? search,
            int? page,
            int? pageSize,
            ConGNoDbContext db,
            CancellationToken ct) =>
        {
            var pageValue = page.GetValueOrDefault(1);
            var sizeValue = pageSize.GetValueOrDefault(20);
            if (pageValue <= 0 || sizeValue <= 0 || sizeValue > 200)
            {
                return ApiErrors.InvalidRequest("Invalid paging parameters.");
            }

            var query = db.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u =>
                    EF.Functions.ILike(u.Username, $"%{term}%") ||
                    EF.Functions.ILike(u.FullName ?? string.Empty, $"%{term}%") ||
                    EF.Functions.ILike(u.Email ?? string.Empty, $"%{term}%"));
            }

            var total = await query.CountAsync(ct);
            var users = await query
                .OrderBy(u => u.Username)
                .Skip((pageValue - 1) * sizeValue)
                .Take(sizeValue)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.FullName,
                    u.Email,
                    u.Phone,
                    u.IsActive,
                    u.ZaloUserId,
                    u.ZaloLinkedAt
                })
                .ToListAsync(ct);

            var userIds = users.Select(u => u.Id).ToList();
            var roleRows = await db.UserRoles
                .AsNoTracking()
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Code })
                .ToListAsync(ct);

            var roleLookup = roleRows
                .GroupBy(r => r.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Code).OrderBy(x => x).ToList());

            var items = users.Select(u => new AdminUserListItem(
                u.Id,
                u.Username,
                u.FullName,
                u.Email,
                u.Phone,
                u.IsActive,
                u.ZaloUserId,
                u.ZaloLinkedAt,
                roleLookup.TryGetValue(u.Id, out var roles) ? roles : Array.Empty<string>()))
                .ToList();

            return Results.Ok(new PagedResult<AdminUserListItem>(items, pageValue, sizeValue, total));
        })
        .WithName("AdminUserList")
        .WithTags("Admin")
        .RequireAuthorization("AdminManage");

        app.MapGet("/admin/roles", async (ConGNoDbContext db, CancellationToken ct) =>
        {
            var roles = await db.Roles
                .AsNoTracking()
                .OrderBy(r => r.Code)
                .Select(r => new AdminRoleItem(r.Id, r.Code, r.Name))
                .ToListAsync(ct);
            return Results.Ok(roles);
        })
        .WithName("AdminRoleList")
        .WithTags("Admin")
        .RequireAuthorization("AdminManage");

        app.MapGet("/admin/health", async (ConGNoDbContext db, CancellationToken ct) =>
        {
            var serverTimeUtc = DateTimeOffset.UtcNow;

            var customersCount = await db.Customers.LongCountAsync(ct);
            var customersLastCreated = await db.Customers.MaxAsync(c => (DateTimeOffset?)c.CreatedAt, ct);
            var customersLastUpdated = await db.Customers.MaxAsync(c => (DateTimeOffset?)c.UpdatedAt, ct);

            var invoicesCount = await db.Invoices.LongCountAsync(ct);
            var invoicesLastCreated = await db.Invoices.MaxAsync(i => (DateTimeOffset?)i.CreatedAt, ct);
            var invoicesLastUpdated = await db.Invoices.MaxAsync(i => (DateTimeOffset?)i.UpdatedAt, ct);

            var advancesCount = await db.Advances.LongCountAsync(ct);
            var advancesLastCreated = await db.Advances.MaxAsync(a => (DateTimeOffset?)a.CreatedAt, ct);
            var advancesLastUpdated = await db.Advances.MaxAsync(a => (DateTimeOffset?)a.UpdatedAt, ct);

            var receiptsCount = await db.Receipts.LongCountAsync(ct);
            var receiptsLastCreated = await db.Receipts.MaxAsync(r => (DateTimeOffset?)r.CreatedAt, ct);
            var receiptsLastUpdated = await db.Receipts.MaxAsync(r => (DateTimeOffset?)r.UpdatedAt, ct);

            var importCount = await db.ImportBatches.LongCountAsync(ct);
            var importLastCreated = await db.ImportBatches.MaxAsync(b => (DateTimeOffset?)b.CreatedAt, ct);
            var importLastUpdated = await db.ImportBatches
                .Select(b => (DateTimeOffset?)(b.CancelledAt ?? b.CommittedAt ?? b.CreatedAt))
                .MaxAsync(ct);

            var reminderCount = await db.ReminderLogs.LongCountAsync(ct);
            var reminderLastCreated = await db.ReminderLogs.MaxAsync(r => (DateTimeOffset?)r.CreatedAt, ct);

            var notificationCount = await db.Notifications.LongCountAsync(ct);
            var notificationLastCreated = await db.Notifications.MaxAsync(n => (DateTimeOffset?)n.CreatedAt, ct);

            var tables = new List<AdminHealthTableSummary>
            {
                new("customers", customersCount, customersLastCreated, customersLastUpdated),
                new("invoices", invoicesCount, invoicesLastCreated, invoicesLastUpdated),
                new("advances", advancesCount, advancesLastCreated, advancesLastUpdated),
                new("receipts", receiptsCount, receiptsLastCreated, receiptsLastUpdated),
                new("import_batches", importCount, importLastCreated, importLastUpdated),
                new("reminder_logs", reminderCount, reminderLastCreated, null),
                new("notifications", notificationCount, notificationLastCreated, null)
            };

            return Results.Ok(new AdminHealthSummary(serverTimeUtc, tables));
        })
        .WithName("AdminHealth")
        .WithTags("Admin")
        .RequireAuthorization("AdminHealthView");

        app.MapPost("/admin/users", async (
            AdminUserCreateRequest request,
            ConGNoDbContext db,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var username = (request.Username ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                return ApiErrors.InvalidRequest("Username is required.");
            }

            var password = request.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(password) || password.Trim().Length < 6)
            {
                return ApiErrors.InvalidRequest("Password must be at least 6 characters.");
            }

            var exists = await db.Users
                .AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct);
            if (exists)
            {
                return ApiErrors.Conflict("Username already exists.");
            }

            var requested = (request.Roles ?? Array.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (requested.Count == 0)
            {
                return ApiErrors.InvalidRequest("Roles are required.");
            }

            var requestedNormalized = requested
                .Select(r => r.ToUpperInvariant())
                .ToHashSet();

            var roles = await db.Roles
                .Where(r => requestedNormalized.Contains(r.Code.ToUpper()))
                .ToListAsync(ct);

            if (roles.Count != requested.Count)
            {
                return ApiErrors.InvalidRequest("Invalid role list.");
            }

            var now = DateTimeOffset.UtcNow;
            var user = new Infrastructure.Data.Entities.User
            {
                Id = Guid.NewGuid(),
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                IsActive = request.IsActive ?? true,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            };

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            db.UserRoles.AddRange(roles.Select(r => new Infrastructure.Data.Entities.UserRole
            {
                UserId = user.Id,
                RoleId = r.Id
            }));

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            await auditService.LogAsync(
                "USER_CREATE",
                "User",
                user.Id.ToString(),
                null,
                new { user.Username, user.FullName, user.Email, user.IsActive, roles = requested },
                ct);

            return Results.Ok(new AdminUserCreateResponse(user.Id));
        })
        .WithName("AdminUserCreate")
        .WithTags("Admin")
        .RequireAuthorization("AdminManage");

        app.MapPut("/admin/users/{id:guid}/roles", async (
            Guid id,
            AdminUserRolesRequest request,
            ConGNoDbContext db,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null)
            {
                return ApiErrors.NotFound("User not found.");
            }

            var requested = request.Roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var requestedNormalized = requested
                .Select(r => r.ToUpperInvariant())
                .ToHashSet();

            var roles = await db.Roles
                .Where(r => requestedNormalized.Contains(r.Code.ToUpper()))
                .ToListAsync(ct);

            if (roles.Count != requested.Count)
            {
                return ApiErrors.InvalidRequest("Invalid role list.");
            }

            var existingRoles = await db.UserRoles
                .Where(ur => ur.UserId == id)
                .ToListAsync(ct);

            var existingRoleIds = existingRoles.Select(r => r.RoleId).ToHashSet();
            var requestedRoleIds = roles.Select(r => r.Id).ToHashSet();

            var toRemove = existingRoles.Where(r => !requestedRoleIds.Contains(r.RoleId)).ToList();
            if (toRemove.Count > 0)
            {
                db.UserRoles.RemoveRange(toRemove);
            }

            var toAdd = roles
                .Where(r => !existingRoleIds.Contains(r.Id))
                .Select(r => new Infrastructure.Data.Entities.UserRole { UserId = id, RoleId = r.Id })
                .ToList();

            if (toAdd.Count > 0)
            {
                db.UserRoles.AddRange(toAdd);
            }

            await db.SaveChangesAsync(ct);

            await auditService.LogAsync(
                "USER_ROLES_UPDATE",
                "User",
                id.ToString(),
                new { roles = existingRoles.Select(r => r.RoleId) },
                new { roles = requestedRoleIds },
                ct);

            return Results.NoContent();
        })
        .WithName("AdminUserRolesUpdate")
        .WithTags("Admin")
        .RequireAuthorization("AdminManage");

        app.MapPut("/admin/users/{id:guid}/status", async (
            Guid id,
            AdminUserStatusRequest request,
            ConGNoDbContext db,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null)
            {
                return ApiErrors.NotFound("User not found.");
            }

            var previous = user.IsActive;
            user.IsActive = request.IsActive;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            await auditService.LogAsync(
                "USER_STATUS_UPDATE",
                "User",
                id.ToString(),
                new { isActive = previous },
                new { isActive = user.IsActive },
                ct);

            return Results.NoContent();
        })
        .WithName("AdminUserStatusUpdate")
        .WithTags("Admin")
        .RequireAuthorization("AdminManage");

        app.MapPut("/admin/users/{id:guid}/zalo", async (
            Guid id,
            AdminUserZaloRequest request,
            ConGNoDbContext db,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null)
            {
                return ApiErrors.NotFound("User not found.");
            }

            var nextId = string.IsNullOrWhiteSpace(request.ZaloUserId)
                ? null
                : request.ZaloUserId.Trim();

            if (!string.IsNullOrWhiteSpace(nextId))
            {
                var exists = await db.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.ZaloUserId == nextId && u.Id != id, ct);
                if (exists)
                {
                    return ApiErrors.Conflict("Zalo user_id already linked to another user.");
                }
            }

            var before = new { user.ZaloUserId, user.ZaloLinkedAt };
            user.ZaloUserId = nextId;
            user.ZaloLinkedAt = nextId is null ? null : DateTimeOffset.UtcNow;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);

            await auditService.LogAsync(
                "USER_ZALO_UPDATE",
                "User",
                id.ToString(),
                before,
                new { user.ZaloUserId, user.ZaloLinkedAt },
                ct);

            return Results.NoContent();
        })
        .WithName("AdminUserZaloUpdate")
        .WithTags("Admin")
        .RequireAuthorization("AdminManage");

        app.MapGet("/admin/audit-logs", async (
            string? entityType,
            string? entityId,
            string? action,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            ConGNoDbContext db,
            CancellationToken ct) =>
        {
            var pageValue = page.GetValueOrDefault(1);
            var sizeValue = pageSize.GetValueOrDefault(50);
            if (pageValue <= 0 || sizeValue <= 0 || sizeValue > 200)
            {
                return ApiErrors.InvalidRequest("Invalid paging parameters.");
            }

            var query = db.AuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                var type = entityType.Trim();
                query = query.Where(a => a.EntityType == type);
            }

            if (!string.IsNullOrWhiteSpace(entityId))
            {
                var idValue = entityId.Trim();
                query = query.Where(a => a.EntityId == idValue);
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                var actionValue = action.Trim();
                query = query.Where(a => a.Action == actionValue);
            }

            if (from.HasValue)
            {
                query = query.Where(a => a.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(a => a.CreatedAt <= to.Value);
            }

            var total = await query.CountAsync(ct);
            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((pageValue - 1) * sizeValue)
                .Take(sizeValue)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    a.Action,
                    a.EntityType,
                    a.EntityId,
                    a.BeforeData,
                    a.AfterData,
                    a.CreatedAt
                })
                .ToListAsync(ct);

            var userIds = logs
                .Select(l => l.UserId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var userLookup = userIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await db.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(
                        u => u.Id,
                        u => string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
                        ct);

            var items = logs.Select(l => new AuditLogListItem(
                    l.Id,
                    l.Action,
                    l.EntityType,
                    l.EntityId,
                    l.UserId.HasValue && userLookup.TryGetValue(l.UserId.Value, out var name) ? name : null,
                    l.CreatedAt,
                    l.BeforeData,
                    l.AfterData))
                .ToList();

            return Results.Ok(new PagedResult<AuditLogListItem>(items, pageValue, sizeValue, total));
        })
        .WithName("AuditLogList")
        .WithTags("Admin")
        .RequireAuthorization("AuditView");

        return app;
    }
}

public sealed record AdminUserListItem(
    Guid Id,
    string Username,
    string? FullName,
    string? Email,
    string? Phone,
    bool IsActive,
    string? ZaloUserId,
    DateTimeOffset? ZaloLinkedAt,
    IReadOnlyList<string> Roles);

public sealed record AdminRoleItem(
    int Id,
    string Code,
    string Name);

public sealed record AdminUserCreateRequest(
    string? Username,
    string? Password,
    string? FullName,
    string? Email,
    string? Phone,
    bool? IsActive,
    IReadOnlyList<string>? Roles);

public sealed record AdminUserCreateResponse(Guid Id);

public sealed record AdminUserRolesRequest(IReadOnlyList<string> Roles);

public sealed record AdminUserStatusRequest(bool IsActive);

public sealed record AdminUserZaloRequest(string? ZaloUserId);

public sealed record AuditLogListItem(
    Guid Id,
    string Action,
    string EntityType,
    string EntityId,
    string? UserName,
    DateTimeOffset CreatedAt,
    string? BeforeData,
    string? AfterData);

public sealed record AdminHealthSummary(
    DateTimeOffset ServerTimeUtc,
    IReadOnlyList<AdminHealthTableSummary> Tables);

public sealed record AdminHealthTableSummary(
    string Name,
    long Count,
    DateTimeOffset? LastCreatedAt,
    DateTimeOffset? LastUpdatedAt);
