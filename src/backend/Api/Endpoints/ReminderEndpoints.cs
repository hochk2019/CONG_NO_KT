using CongNoGolden.Api;
using CongNoGolden.Application.Reminders;

namespace CongNoGolden.Api.Endpoints;

public static class ReminderEndpoints
{
    public static IEndpointRouteBuilder MapReminderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/reminders/settings", async (
            IReminderService service,
            CancellationToken ct) =>
        {
            var result = await service.GetSettingsAsync(ct);
            return Results.Ok(result);
        })
        .WithName("ReminderSettings")
        .WithTags("Reminders")
        .RequireAuthorization("RiskView");

        app.MapPut("/reminders/settings", async (
            ReminderSettingsUpdateRequest request,
            IReminderService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.UpdateSettingsAsync(request, ct);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ReminderSettingsUpdate")
        .WithTags("Reminders")
        .RequireAuthorization("RiskManage");

        app.MapPost("/reminders/run", async (
            IReminderService service,
            CancellationToken ct) =>
        {
            var result = await service.RunAsync(true, ct);
            return Results.Ok(result);
        })
        .WithName("ReminderRun")
        .WithTags("Reminders")
        .RequireAuthorization("RiskManage");

        app.MapGet("/reminders/logs", async (
            string? channel,
            string? status,
            Guid? ownerId,
            int? page,
            int? pageSize,
            IReminderService service,
            CancellationToken ct) =>
        {
            var result = await service.ListLogsAsync(
                new ReminderLogRequest(
                    channel,
                    status,
                    ownerId,
                    page.GetValueOrDefault(1),
                    pageSize.GetValueOrDefault(20)),
                ct);

            return Results.Ok(result);
        })
        .WithName("ReminderLogs")
        .WithTags("Reminders")
        .RequireAuthorization("RiskView");

        return app;
    }
}
