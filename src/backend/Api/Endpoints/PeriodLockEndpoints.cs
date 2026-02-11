using CongNoGolden.Api;
using CongNoGolden.Application.PeriodLocks;
using Microsoft.AspNetCore.Mvc;

namespace CongNoGolden.Api.Endpoints;

public static class PeriodLockEndpoints
{
    public static IEndpointRouteBuilder MapPeriodLockEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/period-locks", async (
            IPeriodLockService service,
            CancellationToken ct) =>
        {
            var result = await service.ListAsync(ct);
            return Results.Ok(result);
        })
        .WithName("PeriodLockList")
        .WithTags("PeriodLocks")
        .RequireAuthorization("PeriodLockManage");

        app.MapPost("/period-locks", async (
            [FromBody] PeriodLockCreateRequest request,
            IPeriodLockService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.LockAsync(request, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("PeriodLockCreate")
        .WithTags("PeriodLocks")
        .RequireAuthorization("PeriodLockManage");

        app.MapPost("/period-locks/{id:guid}/unlock", async (
            Guid id,
            [FromBody] PeriodLockUnlockRequest request,
            IPeriodLockService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.UnlockAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("PeriodLockUnlock")
        .WithTags("PeriodLocks")
        .RequireAuthorization("PeriodLockManage");

        return app;
    }
}
