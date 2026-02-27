using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Customers;
using CongNoGolden.Application.Maintenance;

namespace CongNoGolden.Api.Endpoints;

public static class AdminMaintenanceEndpoints
{
    public static IEndpointRouteBuilder MapAdminMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/health/reconcile-balances/queue", (
            AdminBalanceReconcileRequest? request,
            IMaintenanceJobQueue queue,
            ICurrentUser currentUser) =>
        {
            var applyChanges = request?.ApplyChanges ?? true;
            var maxItems = request?.MaxItems ?? 20;
            var tolerance = request?.Tolerance ?? 0.01m;

            if (maxItems <= 0 || maxItems > 2000)
            {
                return ApiErrors.InvalidRequest("MaxItems must be between 1 and 2000.");
            }

            if (tolerance < 0)
            {
                return ApiErrors.InvalidRequest("Tolerance must be >= 0.");
            }

            var job = queue.Enqueue(new EnqueueMaintenanceJobRequest(
                JobType: MaintenanceJobType.ReconcileBalances,
                ReconcileRequest: new CustomerBalanceReconcileRequest(
                    ApplyChanges: applyChanges,
                    MaxItems: maxItems,
                    Tolerance: tolerance),
                RequestedBy: ResolveRequester(currentUser)));

            return Results.Accepted(
                $"/admin/maintenance/jobs/{job.JobId}",
                new AdminMaintenanceEnqueueResponse(job));
        })
        .WithName("AdminBalanceReconcileQueue")
        .WithTags("Admin")
        .RequireAuthorization("AdminHealthView");

        app.MapPost("/admin/health/run-retention/queue", (
            IMaintenanceJobQueue queue,
            ICurrentUser currentUser) =>
        {
            var job = queue.Enqueue(new EnqueueMaintenanceJobRequest(
                JobType: MaintenanceJobType.RunRetention,
                ReconcileRequest: null,
                RequestedBy: ResolveRequester(currentUser)));

            return Results.Accepted(
                $"/admin/maintenance/jobs/{job.JobId}",
                new AdminMaintenanceEnqueueResponse(job));
        })
        .WithName("AdminDataRetentionQueue")
        .WithTags("Admin")
        .RequireAuthorization("AdminHealthView");

        app.MapGet("/admin/maintenance/jobs", (int? take, IMaintenanceJobQueue queue) =>
        {
            var normalizedTake = take.GetValueOrDefault(20);
            var items = queue.List(normalizedTake);
            return Results.Ok(new AdminMaintenanceJobListResponse(items));
        })
        .WithName("AdminMaintenanceJobList")
        .WithTags("Admin")
        .RequireAuthorization("AdminHealthView");

        app.MapGet("/admin/maintenance/jobs/{jobId:guid}", (Guid jobId, IMaintenanceJobQueue queue) =>
        {
            var job = queue.Get(jobId);
            return job is null
                ? ApiErrors.NotFound("Maintenance job not found.")
                : Results.Ok(new AdminMaintenanceEnqueueResponse(job));
        })
        .WithName("AdminMaintenanceJobDetail")
        .WithTags("Admin")
        .RequireAuthorization("AdminHealthView");

        return app;
    }

    private static string ResolveRequester(ICurrentUser currentUser)
    {
        return string.IsNullOrWhiteSpace(currentUser.Username)
            ? "system"
            : currentUser.Username.Trim();
    }
}

public sealed record AdminMaintenanceEnqueueResponse(MaintenanceJobSnapshot Job);

public sealed record AdminMaintenanceJobListResponse(IReadOnlyList<MaintenanceJobSnapshot> Items);
