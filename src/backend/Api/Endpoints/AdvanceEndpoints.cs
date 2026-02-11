using CongNoGolden.Api;
using CongNoGolden.Application.Advances;
using CongNoGolden.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CongNoGolden.Api.Endpoints;

public static class AdvanceEndpoints
{
    public static IEndpointRouteBuilder MapAdvanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/advances", async (
            string? sellerTaxCode,
            string? customerTaxCode,
            string? status,
            string? advanceNo,
            DateOnly? from,
            DateOnly? to,
            decimal? amountMin,
            decimal? amountMax,
            string? source,
            int? page,
            int? pageSize,
            IAdvanceService service,
            CancellationToken ct) =>
        {
            if (amountMin.HasValue && amountMax.HasValue && amountMin > amountMax)
            {
                return ApiErrors.InvalidRequest("Amount range is invalid: amountMin must be <= amountMax.");
            }
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                return ApiErrors.InvalidRequest("Date range is invalid: from must be <= to.");
            }

            var result = await service.ListAsync(
                new AdvanceListRequest(
                    sellerTaxCode,
                    customerTaxCode,
                    status,
                    advanceNo,
                    from,
                    to,
                    amountMin,
                    amountMax,
                    source,
                    page.GetValueOrDefault(1),
                    pageSize.GetValueOrDefault(20)),
                ct);

            return Results.Ok(result);
        })
        .WithName("AdvanceList")
        .WithTags("Advances")
        .RequireAuthorization("AdvanceManage");

        app.MapPost("/advances", async (
            [FromBody] AdvanceCreateRequest request,
            IAdvanceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CreateAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or ConcurrencyException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("AdvanceCreate")
        .WithTags("Advances")
        .RequireAuthorization("AdvanceManage");

        app.MapPost("/advances/{id:guid}/approve", async (
            Guid id,
            [FromBody] AdvanceApproveRequest request,
            IAdvanceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ApproveAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or ConcurrencyException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("AdvanceApprove")
        .WithTags("Advances")
        .RequireAuthorization("AdvanceManage");

        app.MapPost("/advances/{id:guid}/void", async (
            Guid id,
            [FromBody] AdvanceVoidRequest request,
            IAdvanceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.VoidAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or ConcurrencyException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("AdvanceVoid")
        .WithTags("Advances")
        .RequireAuthorization("AdvanceManage");

        app.MapPut("/advances/{id:guid}", async (
            Guid id,
            [FromBody] AdvanceUpdateRequest request,
            IAdvanceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.UpdateAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or ConcurrencyException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("AdvanceUpdate")
        .WithTags("Advances")
        .RequireAuthorization("AdvanceManage");

        return app;
    }
}
