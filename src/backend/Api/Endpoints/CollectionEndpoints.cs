using CongNoGolden.Api;
using CongNoGolden.Application.Collections;
using CongNoGolden.Application.Risk;

namespace CongNoGolden.Api.Endpoints;

public static class CollectionEndpoints
{
    public static IEndpointRouteBuilder MapCollectionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/collections/tasks", (
            string? status,
            Guid? assignedTo,
            string? search,
            int? take,
            ICollectionTaskQueue queue) =>
        {
            try
            {
                var items = queue.List(new CollectionTaskListRequest(
                    status,
                    assignedTo,
                    search,
                    take.GetValueOrDefault(50)));
                return Results.Ok(items);
            }
            catch (Exception ex)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("CollectionTasksList")
        .WithTags("Collections")
        .RequireAuthorization("RiskView");

        app.MapPost("/collections/tasks/generate", async (
            CollectionTaskGenerateRequest request,
            IRiskService riskService,
            ICollectionTaskQueue queue,
            CancellationToken ct) =>
        {
            try
            {
                DateOnly? asOf = null;
                if (!string.IsNullOrWhiteSpace(request.AsOfDate))
                {
                    if (!DateOnly.TryParse(request.AsOfDate, out var parsed))
                    {
                        return ApiErrors.InvalidRequest("Invalid as_of_date.");
                    }

                    asOf = parsed;
                }

                var take = request.Take.GetValueOrDefault(30);
                var listTake = Math.Clamp(Math.Max(take * 3, 90), 50, 500);
                var riskCustomers = await riskService.ListCustomersAsync(
                    new RiskCustomerListRequest(
                        Search: null,
                        OwnerId: request.OwnerId,
                        Level: null,
                        AsOfDate: asOf,
                        Page: 1,
                        PageSize: listTake,
                        Sort: null,
                        Order: null),
                    ct);

                var minPriorityScore = request.MinPriorityScore.GetValueOrDefault(0.35m);
                var created = queue.EnqueueFromRisk(
                    riskCustomers.Items,
                    take,
                    minPriorityScore,
                    DateTimeOffset.UtcNow);

                var tasks = queue.List(new CollectionTaskListRequest(Take: Math.Clamp(take, 1, 200)));

                return Results.Ok(new
                {
                    created,
                    candidates = riskCustomers.Items.Count,
                    minPriorityScore,
                    tasks
                });
            }
            catch (Exception ex)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("CollectionTasksGenerate")
        .WithTags("Collections")
        .RequireAuthorization("RiskManage");

        app.MapPost("/collections/tasks/{taskId:guid}/assign", (
            Guid taskId,
            CollectionTaskAssignRequest request,
            ICollectionTaskQueue queue) =>
        {
            try
            {
                var task = queue.Assign(taskId, request.AssignedTo, DateTimeOffset.UtcNow);
                return task is null ? Results.NotFound() : Results.Ok(task);
            }
            catch (Exception ex)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("CollectionTaskAssign")
        .WithTags("Collections")
        .RequireAuthorization("RiskManage");

        app.MapPost("/collections/tasks/{taskId:guid}/status", (
            Guid taskId,
            CollectionTaskStatusUpdateRequest request,
            ICollectionTaskQueue queue) =>
        {
            try
            {
                var task = queue.UpdateStatus(taskId, request.Status, request.Note, DateTimeOffset.UtcNow);
                return task is null ? Results.NotFound() : Results.Ok(task);
            }
            catch (Exception ex)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("CollectionTaskStatusUpdate")
        .WithTags("Collections")
        .RequireAuthorization("RiskManage");

        return app;
    }
}
