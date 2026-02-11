using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class BackupSystemNotificationTests
{
    private readonly TestDatabaseFixture _fixture;

    public BackupSystemNotificationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessJobAsync_WhenBackupFails_Creates_System_Notification()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var now = DateTimeOffset.UtcNow;
        var userId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        db.Users.Add(new User
        {
            Id = userId,
            Username = "backup_user",
            PasswordHash = "hash",
            FullName = "Backup User",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        db.BackupSettings.Add(new BackupSettings
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            BackupPath = Path.Combine(Path.GetTempPath(), "congno-backup-tests"),
            RetentionCount = 10,
            ScheduleDayOfWeek = 1,
            ScheduleTime = "02:00",
            PgBinPath = Path.Combine(Path.GetTempPath(), "missing_pg_bin"),
            Timezone = TimeZoneInfo.Local.Id,
            CreatedAt = now,
            UpdatedAt = now
        });

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            Type = "manual",
            Status = "queued",
            CreatedAt = now,
            CreatedBy = userId
        };
        db.BackupJobs.Add(job);

        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _fixture.ConnectionString
            })
            .Build();

        var service = new BackupService(
            db,
            new TestCurrentUser(userId),
            new TestMaintenanceState(),
            new BackupQueue(),
            new BackupProcessRunner(),
            NullLogger<BackupService>.Instance,
            configuration);

        await service.ProcessJobAsync(job.Id, CancellationToken.None);

        var notification = await db.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(n => n.UserId == userId);

        Assert.NotNull(notification);
        Assert.Equal("SYSTEM", notification!.Source);
        Assert.Equal("ALERT", notification.Severity);
        Assert.Contains(job.Id.ToString(), notification.Metadata ?? string.Empty);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.notifications, " +
            "congno.notification_preferences, " +
            "congno.backup_jobs, " +
            "congno.backup_settings, " +
            "congno.users " +
            "RESTART IDENTITY CASCADE;");
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }
        public string? Username => "backup_user";
        public IReadOnlyList<string> Roles => new[] { "Admin" };
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class TestMaintenanceState : IMaintenanceState
    {
        public bool IsActive => false;
        public string? Message => null;
        public void SetActive(bool active, string? message = null) { }
    }
}
