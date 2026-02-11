using CongNoGolden.Api;
using CongNoGolden.Application.Backups;
using CongNoGolden.Application.Common.Interfaces;

namespace CongNoGolden.Api.Endpoints;

public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapBackupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/backup").WithTags("Backup");

        group.MapGet("/settings", async (
            IBackupService service,
            CancellationToken ct) =>
        {
            var settings = await service.GetSettingsAsync(ct);
            return Results.Ok(settings);
        }).RequireAuthorization("BackupManage");

        group.MapPut("/settings", async (
            BackupSettingsUpdateRequest request,
            IBackupService service,
            CancellationToken ct) =>
        {
            try
            {
                var settings = await service.UpdateSettingsAsync(request, ct);
                return Results.Ok(settings);
            }
            catch (InvalidOperationException ex)
            {
                return ApiErrors.InvalidRequest(ex.Message);
            }
        }).RequireAuthorization("BackupManage");

        group.MapPost("/run", async (
            IBackupService service,
            CancellationToken ct) =>
        {
            var job = await service.EnqueueManualBackupAsync(ct);
            return Results.Ok(job);
        }).RequireAuthorization("BackupManage");

        group.MapGet("/jobs", async (
            int? page,
            int? pageSize,
            string? status,
            string? type,
            IBackupService service,
            CancellationToken ct) =>
        {
            var query = new BackupJobQuery(
                page.GetValueOrDefault(1),
                pageSize.GetValueOrDefault(20),
                status,
                type);

            var result = await service.ListJobsAsync(query, ct);
            return Results.Ok(result);
        }).RequireAuthorization("BackupManage");

        group.MapGet("/jobs/{id:guid}", async (
            Guid id,
            IBackupService service,
            CancellationToken ct) =>
        {
            var job = await service.GetJobAsync(id, ct);
            return job is null ? Results.NotFound() : Results.Ok(job);
        }).RequireAuthorization("BackupManage");

        group.MapPost("/jobs/{id:guid}/download-token", async (
            Guid id,
            IBackupService service,
            CancellationToken ct) =>
        {
            try
            {
                var token = await service.IssueDownloadTokenAsync(id, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30), ct);
                return Results.Ok(token);
            }
            catch (InvalidOperationException ex)
            {
                return ApiErrors.InvalidRequest(ex.Message);
            }
        }).RequireAuthorization("BackupManage");

        group.MapGet("/download/{id:guid}", async (
            Guid id,
            string token,
            IBackupService service,
            CancellationToken ct) =>
        {
            var stream = await service.OpenDownloadStreamAsync(id, token, DateTimeOffset.UtcNow, ct);
            if (stream is null)
            {
                return Results.NotFound();
            }

            return Results.File(stream, "application/octet-stream", $"backup_{id}.dump");
        }).RequireAuthorization("BackupManage");

        group.MapPost("/upload", async (
            IFormFile file,
            IBackupService service,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return ApiErrors.InvalidRequest("File is required.");
            }

            if (!Path.GetExtension(file.FileName).Equals(".dump", StringComparison.OrdinalIgnoreCase))
            {
                return ApiErrors.InvalidRequest("Invalid file type.");
            }

            await using var stream = file.OpenReadStream();
            var result = await service.UploadAsync(file.FileName, file.Length, stream, ct);
            return Results.Ok(result);
        }).RequireAuthorization("BackupRestore")
        .DisableAntiforgery();

        group.MapPost("/restore", async (
            BackupRestoreRequest request,
            IBackupService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.RestoreAsync(request, ct);
                return Results.Ok(new { status = "ok" });
            }
            catch (InvalidOperationException ex)
            {
                return ApiErrors.InvalidRequest(ex.Message);
            }
        }).RequireAuthorization("BackupRestore");

        group.MapGet("/audit", async (
            int? page,
            int? pageSize,
            IBackupService service,
            CancellationToken ct) =>
        {
            var result = await service.ListAuditAsync(page.GetValueOrDefault(1), pageSize.GetValueOrDefault(20), ct);
            return Results.Ok(result);
        }).RequireAuthorization("BackupManage");

        group.MapGet("/status", (IMaintenanceState state) =>
            Results.Ok(new { maintenance = state.IsActive, message = state.Message }))
            .RequireAuthorization("BackupManage");

        return app;
    }
}
