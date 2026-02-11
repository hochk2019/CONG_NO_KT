using CongNoGolden.Api;
using CongNoGolden.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace CongNoGolden.Api.Endpoints;

public static class LookupEndpoints
{
    private static readonly string[] OwnerRoles = { "ADMIN", "SUPERVISOR", "ACCOUNTANT" };

    public static IEndpointRouteBuilder MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/lookups/sellers", async (
            string? search,
            int? limit,
            ConGNoDbContext db,
            CancellationToken ct) =>
        {
            var take = NormalizeLimit(limit);
            if (take is null)
            {
                return ApiErrors.InvalidRequest("Invalid limit parameter.");
            }

            var query = db.Sellers.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var pattern = $"%{term}%";
                query = query.Where(s =>
                    EF.Functions.ILike(s.SellerTaxCode, pattern) ||
                    EF.Functions.ILike(s.Name, pattern));
            }

            var items = await query
                .OrderBy(s => s.Name)
                .ThenBy(s => s.SellerTaxCode)
                .Take(take.Value)
                .Select(s => new SellerLookupItem(s.SellerTaxCode, s.Name, s.ShortName))
                .ToListAsync(ct);

            return Results.Ok(items);
        })
        .WithName("LookupSellers")
        .WithTags("Lookups")
        .RequireAuthorization("CustomerView");

        app.MapGet("/lookups/customers", async (
            string? search,
            int? limit,
            ConGNoDbContext db,
            CancellationToken ct) =>
        {
            var take = NormalizeLimit(limit);
            if (take is null)
            {
                return ApiErrors.InvalidRequest("Invalid limit parameter.");
            }

            var query = db.Customers.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var pattern = $"%{term}%";
                var namePattern = $"%{term.ToLowerInvariant()}%";
                query = query.Where(c =>
                    EF.Functions.ILike(c.TaxCode, pattern) ||
                    EF.Functions.ILike(
                        EF.Property<string>(c, "NameSearch"),
                        NpgsqlFullTextSearchDbFunctionsExtensions.Unaccent(EF.Functions, namePattern)));
            }

            var items = await query
                .OrderBy(c => c.Name)
                .ThenBy(c => c.TaxCode)
                .Take(take.Value)
                .Select(c => new CustomerLookupItem(c.TaxCode, c.Name))
                .ToListAsync(ct);

            return Results.Ok(items);
        })
        .WithName("LookupCustomers")
        .WithTags("Lookups")
        .RequireAuthorization("CustomerView");

        app.MapGet("/lookups/owners", async (
            string? search,
            int? limit,
            ConGNoDbContext db,
            CancellationToken ct) =>
        {
            var take = NormalizeLimit(limit);
            if (take is null)
            {
                return ApiErrors.InvalidRequest("Invalid limit parameter.");
            }

            var ownerIds = await db.UserRoles
                .AsNoTracking()
                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Code })
                .Where(row => OwnerRoles.Contains(row.Code.ToUpper()))
                .Select(row => row.UserId)
                .Distinct()
                .ToListAsync(ct);

            var query = db.Users.AsNoTracking()
                .Where(u => u.IsActive && ownerIds.Contains(u.Id));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var pattern = $"%{term}%";
                query = query.Where(u =>
                    EF.Functions.ILike(u.Username, pattern) ||
                    EF.Functions.ILike(u.FullName ?? string.Empty, pattern) ||
                    EF.Functions.ILike(u.Email ?? string.Empty, pattern));
            }

            var items = await query
                .OrderBy(u => u.FullName ?? u.Username)
                .ThenBy(u => u.Username)
                .Take(take.Value)
                .Select(u => new OwnerLookupItem(
                    u.Id,
                    string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
                    u.Username))
                .ToListAsync(ct);

            return Results.Ok(items);
        })
        .WithName("LookupOwners")
        .WithTags("Lookups")
        .RequireAuthorization("CustomerView");

        app.MapGet("/lookups/users", async (
            string? search,
            int? limit,
            ConGNoDbContext db,
            CancellationToken ct) =>
        {
            var take = NormalizeLimit(limit);
            if (take is null)
            {
                return ApiErrors.InvalidRequest("Invalid limit parameter.");
            }

            var query = db.Users.AsNoTracking().Where(u => u.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var pattern = $"%{term}%";
                query = query.Where(u =>
                    EF.Functions.ILike(u.Username, pattern) ||
                    EF.Functions.ILike(u.FullName ?? string.Empty, pattern) ||
                    EF.Functions.ILike(u.Email ?? string.Empty, pattern));
            }

            var items = await query
                .OrderBy(u => u.FullName ?? u.Username)
                .ThenBy(u => u.Username)
                .Take(take.Value)
                .Select(u => new UserLookupItem(
                    u.Id,
                    string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
                    u.Username))
                .ToListAsync(ct);

            return Results.Ok(items);
        })
        .WithName("LookupUsers")
        .WithTags("Lookups")
        .RequireAuthorization("CustomerView");

        return app;
    }

    private static int? NormalizeLimit(int? limit)
    {
        var value = limit.GetValueOrDefault(50);
        if (value <= 0 || value > 200)
        {
            return null;
        }

        return value;
    }
}

public sealed record SellerLookupItem(string TaxCode, string Name, string? ShortName);
public sealed record CustomerLookupItem(string TaxCode, string Name);
public sealed record OwnerLookupItem(Guid Id, string Name, string Username);
public sealed record UserLookupItem(Guid Id, string Name, string Username);
