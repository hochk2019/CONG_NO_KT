using CongNoGolden.Api;
using CongNoGolden.Application.Notifications;

namespace CongNoGolden.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications", async (
            bool? unreadOnly,
            string? source,
            string? severity,
            string? q,
            int? page,
            int? pageSize,
            INotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.ListAsync(
                new NotificationListRequest(
                    unreadOnly,
                    source,
                    severity,
                    q,
                    page.GetValueOrDefault(1),
                    pageSize.GetValueOrDefault(20)),
                ct);

            return Results.Ok(result);
        })
        .WithName("Notifications")
        .WithTags("Notifications")
        .RequireAuthorization();

        app.MapPost("/notifications/{id}/read", async (
            Guid id,
            INotificationService service,
            CancellationToken ct) =>
        {
            await service.MarkReadAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("NotificationRead")
        .WithTags("Notifications")
        .RequireAuthorization();

        app.MapGet("/notifications/unread-count", async (
            INotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.GetUnreadCountAsync(ct);
            return Results.Ok(result);
        })
        .WithName("NotificationUnreadCount")
        .WithTags("Notifications")
        .RequireAuthorization();

        app.MapPost("/notifications/read-all", async (
            INotificationService service,
            CancellationToken ct) =>
        {
            await service.MarkAllReadAsync(ct);
            return Results.NoContent();
        })
        .WithName("NotificationReadAll")
        .WithTags("Notifications")
        .RequireAuthorization();

        app.MapGet("/notifications/preferences", async (
            INotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.GetPreferencesAsync(ct);
            return Results.Ok(result);
        })
        .WithName("NotificationPreferences")
        .WithTags("Notifications")
        .RequireAuthorization();

        app.MapPut("/notifications/preferences", async (
            NotificationPreferencesUpdate request,
            INotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdatePreferencesAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("NotificationPreferencesUpdate")
        .WithTags("Notifications")
        .RequireAuthorization();

        return app;
    }
}
