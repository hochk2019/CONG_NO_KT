using CongNoGolden.Api;
using CongNoGolden.Application.Risk;

namespace CongNoGolden.Api.Endpoints;

public static class RiskEndpoints
{
    public static IEndpointRouteBuilder MapRiskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/risk/overview", async (
            string? asOfDate,
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

            var result = await service.GetOverviewAsync(new RiskOverviewRequest(asOf), ct);
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
}
