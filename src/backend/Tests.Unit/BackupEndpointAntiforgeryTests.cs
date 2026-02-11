using System.Linq;
using CongNoGolden.Api.Endpoints;
using CongNoGolden.Application.Backups;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Antiforgery;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupEndpointAntiforgeryTests
{
    [Fact]
    public void UploadEndpoint_DisablesAntiforgery()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IBackupService, StubBackupService>();
        builder.Services.AddSingleton<IMaintenanceState, StubMaintenanceState>();
        var app = builder.Build();

        app.MapBackupEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .First(e => e.RoutePattern.RawText == "/admin/backup/upload");

        var metadata = endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>();

        Assert.NotNull(metadata);
        Assert.False(metadata!.RequiresValidation);
    }

    private sealed class StubBackupService : IBackupService
    {
        public Task<BackupSettingsDto> GetSettingsAsync(CancellationToken ct) =>
            Task.FromResult(new BackupSettingsDto(false, "path", 10, 1, "02:00", "UTC", "bin", null));

        public Task<BackupSettingsDto> UpdateSettingsAsync(BackupSettingsUpdateRequest request, CancellationToken ct) =>
            Task.FromResult(new BackupSettingsDto(request.Enabled, request.BackupPath, request.RetentionCount, request.ScheduleDayOfWeek, request.ScheduleTime, "UTC", request.PgBinPath, null));

        public Task<BackupJobListItem> EnqueueManualBackupAsync(CancellationToken ct) =>
            Task.FromResult(new BackupJobListItem(Guid.NewGuid(), "manual", "queued", DateTimeOffset.UtcNow, null, null, null, null, null, null));

        public Task<PagedResult<BackupJobListItem>> ListJobsAsync(BackupJobQuery query, CancellationToken ct) =>
            Task.FromResult(new PagedResult<BackupJobListItem>(new List<BackupJobListItem>(), query.Page, query.PageSize, 0));

        public Task<BackupJobDetail?> GetJobAsync(Guid jobId, CancellationToken ct) =>
            Task.FromResult<BackupJobDetail?>(null);

        public Task<BackupDownloadToken> IssueDownloadTokenAsync(Guid jobId, DateTimeOffset now, TimeSpan ttl, CancellationToken ct) =>
            Task.FromResult(new BackupDownloadToken("token", now.Add(ttl)));

        public Task<Stream?> OpenDownloadStreamAsync(Guid jobId, string token, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult<Stream?>(null);

        public Task<BackupUploadResult> UploadAsync(string fileName, long fileSize, Stream stream, CancellationToken ct) =>
            Task.FromResult(new BackupUploadResult(Guid.NewGuid(), fileName, fileSize, DateTimeOffset.UtcNow.AddMinutes(30)));

        public Task RestoreAsync(BackupRestoreRequest request, CancellationToken ct) => Task.CompletedTask;

        public Task<PagedResult<BackupAuditItem>> ListAuditAsync(int page, int pageSize, CancellationToken ct) =>
            Task.FromResult(new PagedResult<BackupAuditItem>(new List<BackupAuditItem>(), page, pageSize, 0));

        public Task<bool> IsMaintenanceModeAsync(CancellationToken ct) => Task.FromResult(false);

        public Task<bool> HasPendingScheduledBackupAsync(CancellationToken ct) => Task.FromResult(false);

        public Task EnqueueScheduledBackupAsync(CancellationToken ct) => Task.CompletedTask;
        public Task ProcessJobAsync(Guid jobId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubMaintenanceState : IMaintenanceState
    {
        public bool IsActive => false;
        public string? Message => null;
        public void SetActive(bool active, string? message = null) { }
    }
}
