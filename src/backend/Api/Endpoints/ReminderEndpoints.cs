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
            ReminderRunRequest? request,
            IReminderService service,
            CancellationToken ct) =>
        {
            var result = await service.RunAsync(request ?? new ReminderRunRequest(), ct);
            return Results.Ok(result);
        })
        .WithName("ReminderRun")
        .WithTags("Reminders")
        .RequireAuthorization("RiskManage");

        app.MapGet("/reminders/response-state", async (
            string customerTaxCode,
            string channel,
            IReminderService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetResponseStateAsync(customerTaxCode, channel, ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ReminderResponseStateGet")
        .WithTags("Reminders")
        .RequireAuthorization("RiskView");

        app.MapPut("/reminders/response-state", async (
            ReminderResponseStateUpsertRequest request,
            IReminderService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.UpsertResponseStateAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ReminderResponseStateUpdate")
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
