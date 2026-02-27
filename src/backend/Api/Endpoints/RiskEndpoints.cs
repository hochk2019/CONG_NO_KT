using CongNoGolden.Api;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Notifications;
using CongNoGolden.Application.Reminders;
using CongNoGolden.Application.Risk;
using CongNoGolden.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Api.Endpoints;

public static class RiskEndpoints
{
    public static IEndpointRouteBuilder MapRiskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/risk/overview", async (
            string? asOfDate,
            IRiskService service,
            IReadModelCache cache,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            DateOnly? asOf = null;
            if (!string.IsNullOrWhiteSpace(asOfDate))
            {
                if (!DateOnly.TryParse(asOfDate, out var parsed))
                {
                    return ApiErrors.InvalidRequest("Invalid asOfDate.");
                }
                asOf = parsed;
            }

            var cacheKey = EndpointCacheKeys.ForHttpRequest(httpContext);
            var result = await cache.GetOrCreateAsync(
                "risk",
                cacheKey,
                TimeSpan.FromSeconds(30),
                token => service.GetOverviewAsync(new RiskOverviewRequest(asOf), token),
                ct);
            return Results.Ok(result);
        })
        .WithName("RiskOverview")
        .WithTags("Risk")
        .RequireAuthorization("RiskView");

        app.MapGet("/risk/customers", async (
            string? search,
            Guid? ownerId,
            string? level,
            string? asOfDate,
            int? page,
            int? pageSize,
            string? sort,
            string? order,
            IRiskService service,
            CancellationToken ct) =>
        {
            DateOnly? asOf = null;
            if (!string.IsNullOrWhiteSpace(asOfDate))
            {
                if (!DateOnly.TryParse(asOfDate, out var parsed))
                {
                    return ApiErrors.InvalidRequest("Invalid asOfDate.");
                }
                asOf = parsed;
            }

            var result = await service.ListCustomersAsync(
                new RiskCustomerListRequest(
                    search,
                    ownerId,
                    level,
                    asOf,
                    page.GetValueOrDefault(1),
                    pageSize.GetValueOrDefault(20),
                    sort,
                    order),
                ct);

            return Results.Ok(result);
        })
        .WithName("RiskCustomers")
        .WithTags("Risk")
        .RequireAuthorization("RiskView");

        app.MapGet("/risk/delta-alerts", async (
            string? status,
            string? customerTaxCode,
            string? fromDate,
            string? toDate,
            int? page,
            int? pageSize,
            IRiskService service,
            CancellationToken ct) =>
        {
            DateOnly? from = null;
            if (!string.IsNullOrWhiteSpace(fromDate))
            {
                if (!DateOnly.TryParse(fromDate, out var parsed))
                {
                    return ApiErrors.InvalidRequest("Invalid fromDate.");
                }

                from = parsed;
            }

            DateOnly? to = null;
            if (!string.IsNullOrWhiteSpace(toDate))
            {
                if (!DateOnly.TryParse(toDate, out var parsed))
                {
                    return ApiErrors.InvalidRequest("Invalid toDate.");
                }

                to = parsed;
            }

            var result = await service.ListDeltaAlertsAsync(
                new RiskDeltaAlertListRequest(
                    status,
                    customerTaxCode,
                    from,
                    to,
                    page.GetValueOrDefault(1),
                    pageSize.GetValueOrDefault(20)),
                ct);

            return Results.Ok(result);
        })
        .WithName("RiskDeltaAlerts")
        .WithTags("Risk")
        .RequireAuthorization("RiskView");

        app.MapGet("/risk/{customerTaxCode}/score-history", async (
            string customerTaxCode,
            string? fromDate,
            string? toDate,
            int? take,
            IRiskService service,
            CancellationToken ct) =>
        {
            DateOnly? from = null;
            if (!string.IsNullOrWhiteSpace(fromDate))
            {
                if (!DateOnly.TryParse(fromDate, out var parsed))
                {
                    return ApiErrors.InvalidRequest("Invalid fromDate.");
                }

                from = parsed;
            }

            DateOnly? to = null;
            if (!string.IsNullOrWhiteSpace(toDate))
            {
                if (!DateOnly.TryParse(toDate, out var parsed))
                {
                    return ApiErrors.InvalidRequest("Invalid toDate.");
                }

                to = parsed;
            }

            var result = await service.GetScoreHistoryAsync(
                customerTaxCode,
                from,
                to,
                take.GetValueOrDefault(90),
                ct);

            return Results.Ok(result);
        })
        .WithName("RiskScoreHistory")
        .WithTags("Risk")
        .RequireAuthorization("RiskView");

        app.MapGet("/risk/bootstrap", async (
            string? search,
            Guid? ownerId,
            string? level,
            string? asOfDate,
            int? page,
            int? pageSize,
            string? sort,
            string? order,
            string? logChannel,
            string? logStatus,
            int? logPage,
            int? logPageSize,
            int? notificationPage,
            int? notificationPageSize,
            IRiskService service,
            IReminderService reminderService,
            INotificationService notificationService,
            ConGNoDbContext db,
            ICurrentUser currentUser,
            IReadModelCache cache,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            DateOnly? asOf = null;
            if (!string.IsNullOrWhiteSpace(asOfDate))
            {
                if (!DateOnly.TryParse(asOfDate, out var parsed))
                {
                    return ApiErrors.InvalidRequest("Invalid asOfDate.");
                }

                asOf = parsed;
            }

            var cacheKey = EndpointCacheKeys.ForHttpRequest(httpContext);
            var request = new RiskBootstrapRequest(
                search,
                ownerId,
                level,
                asOf,
                page.GetValueOrDefault(1),
                pageSize.GetValueOrDefault(20),
                sort,
                order,
                logChannel,
                logStatus,
                logPage.GetValueOrDefault(1),
                logPageSize.GetValueOrDefault(20),
                notificationPage.GetValueOrDefault(1),
                notificationPageSize.GetValueOrDefault(5));

            var response = await cache.GetOrCreateAsync(
                "risk",
                cacheKey,
                TimeSpan.FromSeconds(45),
                token => BuildRiskBootstrapResponseAsync(
                    request,
                    service,
                    reminderService,
                    notificationService,
                    innerToken => GetZaloStatusAsync(db, currentUser, innerToken),
                    token),
                ct);

            return Results.Ok(response);
        })
        .WithName("RiskBootstrap")
        .WithTags("Risk")
        .RequireAuthorization("RiskView");

        app.MapGet("/risk/rules", async (IRiskService service, CancellationToken ct) =>
        {
            var rules = await service.GetRulesAsync(ct);
            return Results.Ok(rules);
        })
        .WithName("RiskRules")
        .WithTags("Risk")
        .RequireAuthorization("RiskView");

        app.MapPut("/risk/rules", async (
            RiskRulesUpdateRequest request,
            IRiskService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.UpdateRulesAsync(request, ct);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("RiskRulesUpdate")
        .WithTags("Risk")
        .RequireAuthorization("RiskManage");

        return app;
    }

    internal static async Task<RiskBootstrapResponse> BuildRiskBootstrapResponseAsync(
        RiskBootstrapRequest request,
        IRiskService service,
        IReminderService reminderService,
        INotificationService notificationService,
        Func<CancellationToken, Task<ZaloLinkStatusResponse?>> loadZaloStatus,
        CancellationToken ct)
    {
        // Run sequentially to avoid concurrent EF operations on the same scoped DbContext.
        var overview = await service.GetOverviewAsync(new RiskOverviewRequest(request.AsOfDate), ct);
        var customers = await service.ListCustomersAsync(
            new RiskCustomerListRequest(
                request.Search,
                request.OwnerId,
                request.Level,
                request.AsOfDate,
                request.Page,
                request.PageSize,
                request.Sort,
                request.Order),
            ct);
        var rules = await service.GetRulesAsync(ct);
        var settings = await reminderService.GetSettingsAsync(ct);
        var logs = await reminderService.ListLogsAsync(
            new ReminderLogRequest(
                request.LogChannel,
                request.LogStatus,
                request.OwnerId,
                request.LogPage,
                request.LogPageSize),
            ct);
        var notifications = await notificationService.ListAsync(
            new NotificationListRequest(
                true,
                null,
                null,
                null,
                request.NotificationPage,
                request.NotificationPageSize),
            ct);
        var zaloStatus = await loadZaloStatus(ct);

        return new RiskBootstrapResponse(
            overview,
            customers,
            rules,
            settings,
            logs,
            notifications,
            zaloStatus);
    }

    private static async Task<ZaloLinkStatusResponse?> GetZaloStatusAsync(
        ConGNoDbContext db,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
        {
            return null;
        }

        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => new { u.ZaloUserId, u.ZaloLinkedAt })
            .FirstOrDefaultAsync(ct);

        return user is null
            ? null
            : new ZaloLinkStatusResponse(
                !string.IsNullOrWhiteSpace(user.ZaloUserId),
                user.ZaloUserId,
                user.ZaloLinkedAt);
    }
}

internal sealed record RiskBootstrapRequest(
    string? Search,
    Guid? OwnerId,
    string? Level,
    DateOnly? AsOfDate,
    int Page,
    int PageSize,
    string? Sort,
    string? Order,
    string? LogChannel,
    string? LogStatus,
    int LogPage,
    int LogPageSize,
    int NotificationPage,
    int NotificationPageSize);

public sealed record RiskBootstrapResponse(
    RiskOverviewDto Overview,
    PagedResult<RiskCustomerItem> Customers,
    IReadOnlyList<RiskRuleDto> Rules,
    ReminderSettingsDto Settings,
    PagedResult<ReminderLogItem> Logs,
    PagedResult<NotificationItem> Notifications,
    ZaloLinkStatusResponse? ZaloStatus);
