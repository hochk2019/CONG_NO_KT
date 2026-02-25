using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Reports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class ReportScheduleServiceTests
{
    private readonly TestDatabaseFixture _fixture;

    public ReportScheduleServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RunNow_WritesRunLog_AndReportNotification()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("91000000-0000-0000-0000-000000000001");
        var now = DateTimeOffset.UtcNow;

        db.Users.Add(new User
        {
            Id = userId,
            Username = "report_owner",
            PasswordHash = "hash",
            FullName = "Report Owner",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });
        await db.SaveChangesAsync();

        var currentUser = new TestCurrentUser(userId, new[] { "Admin" });
        var exportService = new FakeReportExportService();
        var service = new ReportScheduleService(
            db,
            currentUser,
            exportService,
            NullLogger<ReportScheduleService>.Instance);

        var schedule = await service.CreateAsync(
            new ReportDeliveryScheduleUpsertRequest(
                ReportExportKind.Summary,
                ReportExportFormat.Xlsx,
                "* * * * *",
                "UTC",
                new[] { "acct@example.local" },
                new ReportDeliveryFilterDto(
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 1, 31),
                    null,
                    null,
                    "CUST-RS-01",
                    null,
                    null),
                true),
            CancellationToken.None);

        var run = await service.RunNowAsync(schedule.Id, CancellationToken.None);

        Assert.Equal("SUCCEEDED", run.Status);
        Assert.NotNull(run.Artifact);
        Assert.Equal("summary-report.xlsx", run.Artifact!.FileName);
        Assert.Single(exportService.Requests);
        Assert.Equal(ReportExportKind.Summary, exportService.Requests[0].Kind);

        var runEntity = await db.ReportDeliveryRuns.AsNoTracking().SingleAsync();
        Assert.Equal("SUCCEEDED", runEntity.Status);
        Assert.NotNull(runEntity.FinishedAt);
        Assert.False(string.IsNullOrWhiteSpace(runEntity.ArtifactMeta));

        var scheduleEntity = await db.ReportDeliverySchedules.AsNoTracking().SingleAsync();
        Assert.NotNull(scheduleEntity.LastRunAt);
        Assert.NotNull(scheduleEntity.NextRunAt);
        Assert.True(scheduleEntity.NextRunAt > scheduleEntity.LastRunAt);

        var notification = await db.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal(userId, notification.UserId);
        Assert.Equal("REPORT", notification.Source);
        Assert.Equal("INFO", notification.Severity);
    }

    [Fact]
    public async Task RunDueSchedules_ExecutesOnlyDueSchedules()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("91000000-0000-0000-0000-000000000002");
        var now = DateTimeOffset.UtcNow;

        db.Users.Add(new User
        {
            Id = userId,
            Username = "report_scheduler",
            PasswordHash = "hash",
            FullName = "Report Scheduler",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });
        await db.SaveChangesAsync();

        var currentUser = new TestCurrentUser(userId, new[] { "Admin" });
        var exportService = new FakeReportExportService();
        var service = new ReportScheduleService(
            db,
            currentUser,
            exportService,
            NullLogger<ReportScheduleService>.Instance);

        var due = await service.CreateAsync(
            new ReportDeliveryScheduleUpsertRequest(
                ReportExportKind.Summary,
                ReportExportFormat.Xlsx,
                "* * * * *",
                "UTC",
                Array.Empty<string>(),
                null,
                true),
            CancellationToken.None);

        var future = await service.CreateAsync(
            new ReportDeliveryScheduleUpsertRequest(
                ReportExportKind.Summary,
                ReportExportFormat.Xlsx,
                "* * * * *",
                "UTC",
                Array.Empty<string>(),
                null,
                true),
            CancellationToken.None);

        var dueEntity = await db.ReportDeliverySchedules.SingleAsync(x => x.Id == due.Id);
        var futureEntity = await db.ReportDeliverySchedules.SingleAsync(x => x.Id == future.Id);
        dueEntity.NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        futureEntity.NextRunAt = DateTimeOffset.UtcNow.AddMinutes(30);
        await db.SaveChangesAsync();

        var executed = await service.RunDueSchedulesAsync(20, CancellationToken.None);

        Assert.Equal(1, executed);

        var dueRuns = await db.ReportDeliveryRuns.AsNoTracking().Where(x => x.ScheduleId == due.Id).CountAsync();
        var futureRuns = await db.ReportDeliveryRuns.AsNoTracking().Where(x => x.ScheduleId == future.Id).CountAsync();
        Assert.Equal(1, dueRuns);
        Assert.Equal(0, futureRuns);

        var refreshedDue = await db.ReportDeliverySchedules.AsNoTracking().SingleAsync(x => x.Id == due.Id);
        var refreshedFuture = await db.ReportDeliverySchedules.AsNoTracking().SingleAsync(x => x.Id == future.Id);
        Assert.NotNull(refreshedDue.LastRunAt);
        Assert.NotNull(refreshedDue.NextRunAt);
        Assert.True(refreshedDue.NextRunAt > refreshedDue.LastRunAt);
        Assert.Null(refreshedFuture.LastRunAt);

        var notifications = await db.Notifications.AsNoTracking().CountAsync();
        Assert.Equal(1, notifications);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.report_delivery_runs, " +
            "congno.report_delivery_schedules, " +
            "congno.notifications, " +
            "congno.users " +
            "RESTART IDENTITY CASCADE;");
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid userId, IReadOnlyList<string> roles)
        {
            UserId = userId;
            Roles = roles;
        }

        public Guid? UserId { get; }
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class FakeReportExportService : IReportExportService
    {
        public List<ReportExportRequest> Requests { get; } = [];

        public Task<ReportExportResult> ExportAsync(ReportExportRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new ReportExportResult(
                Content: new byte[] { 1, 2, 3, 4 },
                FileName: "summary-report.xlsx"));
        }
    }
}
