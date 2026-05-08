using System.Diagnostics;
using System.Text.RegularExpressions;
using CongNoGolden.Application.Backups;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupServiceRecoveryAndOffsiteFlowTests
{
    [Fact]
    public async Task ProcessNextPendingJobAsync_ProcessesQueuedJobWithoutInMemorySignal()
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"backup_process_next_{Guid.NewGuid():N}")
            .Options;

        var backupPath = Path.Combine(Path.GetTempPath(), $"congno-unit-backups-{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupPath);

        try
        {
            await using var db = new ConGNoDbContext(options);
            var now = DateTimeOffset.UtcNow;
            db.BackupSettings.Add(new BackupSettings
            {
                Id = Guid.NewGuid(),
                Enabled = true,
                BackupPath = backupPath,
                RetentionCount = 10,
                ScheduleDayOfWeek = 1,
                ScheduleTime = "02:00",
                Timezone = "UTC",
                PgBinPath = OperatingSystem.IsWindows() ? @"C:\Program Files\PostgreSQL\16\bin" : "/usr/bin",
                CreatedAt = now,
                UpdatedAt = now
            });

            var job = new BackupJob
            {
                Id = Guid.NewGuid(),
                Type = "manual",
                Status = "queued",
                CreatedAt = now
            };
            db.BackupJobs.Add(job);
            await db.SaveChangesAsync();

            var service = new BackupService(
                db,
                new TestCurrentUser(),
                new TestMaintenanceState(),
                new BackupQueue(),
                new FakeBackupProcessRunner(),
                new TrackingBackupOffsiteService(),
                NullLogger<BackupService>.Instance,
                CreateConfiguration());

            var processed = await service.ProcessNextPendingJobAsync(CancellationToken.None);

            Assert.True(processed);

            var refreshed = await db.BackupJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id);
            Assert.Equal("success", refreshed.Status);
            Assert.NotNull(refreshed.FilePath);
            Assert.True(File.Exists(refreshed.FilePath));
        }
        finally
        {
            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProcessJobAsync_WhenLocalBackupSucceeds_EnqueuesSingleOffsiteUpload()
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"backup_offsite_enqueue_{Guid.NewGuid():N}")
            .Options;

        var backupPath = Path.Combine(Path.GetTempPath(), $"congno-unit-backups-{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupPath);

        try
        {
            await using var db = new ConGNoDbContext(options);
            var now = DateTimeOffset.UtcNow;

            db.BackupSettings.Add(new BackupSettings
            {
                Id = Guid.NewGuid(),
                Enabled = true,
                BackupPath = backupPath,
                RetentionCount = 10,
                ScheduleDayOfWeek = 1,
                ScheduleTime = "02:00",
                Timezone = "UTC",
                PgBinPath = OperatingSystem.IsWindows() ? @"C:\Program Files\PostgreSQL\16\bin" : "/usr/bin",
                OffsiteEnabled = true,
                OffsiteProvider = "google_drive",
                GoogleDriveFolderId = "drive-folder-001",
                OffsiteRetentionCount = 7,
                UploadAfterBackup = true,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.BackupOffsiteConnections.Add(new BackupOffsiteConnection
            {
                Id = Guid.NewGuid(),
                Provider = "google_drive",
                Status = "connected",
                EncryptedRefreshToken = "ciphertext",
                GoogleDriveFolderId = "drive-folder-001",
                ConnectedAt = now,
                LastValidatedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });

            var job = new BackupJob
            {
                Id = Guid.NewGuid(),
                Type = "manual",
                Status = "queued",
                CreatedAt = now
            };
            db.BackupJobs.Add(job);
            await db.SaveChangesAsync();

            var offsiteService = new TrackingBackupOffsiteService();
            var service = new BackupService(
                db,
                new TestCurrentUser(),
                new TestMaintenanceState(),
                new BackupQueue(),
                new FakeBackupProcessRunner(),
                offsiteService,
                NullLogger<BackupService>.Instance,
                CreateConfiguration());

            await service.ProcessJobAsync(job.Id, CancellationToken.None);

            var refreshed = await db.BackupJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id);
            Assert.Equal("success", refreshed.Status);
            Assert.Single(offsiteService.EnqueuedBackupJobIds);
            Assert.Equal(job.Id, offsiteService.EnqueuedBackupJobIds[0]);
        }
        finally
        {
            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, recursive: true);
            }
        }
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=congno_test;Username=postgres;Password=postgres"
            })
            .Build();
    }

    private sealed class FakeBackupProcessRunner : IBackupProcessRunner
    {
        private static readonly Regex FilePathRegex = new("-f\\s+\"(?<path>[^\"]+)\"", RegexOptions.Compiled);

        public Task<BackupProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken ct)
        {
            var match = FilePathRegex.Match(startInfo.Arguments);
            if (match.Success)
            {
                var filePath = match.Groups["path"].Value;
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                File.WriteAllText(filePath, "unit-backup");
            }

            return Task.FromResult(new BackupProcessResult(0, "ok", string.Empty));
        }
    }

    private sealed class TrackingBackupOffsiteService : IBackupOffsiteService
    {
        public List<Guid> EnqueuedBackupJobIds { get; } = [];

        public Task<string> BuildGoogleDriveConnectUrlAsync(string redirectUri, string? folderId, CancellationToken ct) =>
            Task.FromResult("https://example.test/connect");

        public Task<BackupOffsiteConnectionStatus> CompleteGoogleDriveCallbackAsync(string code, string redirectUri, string? folderId, CancellationToken ct) =>
            Task.FromResult(new BackupOffsiteConnectionStatus(true, "google_drive", folderId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));

        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public Task EnqueueUploadForBackupJobAsync(Guid backupJobId, CancellationToken ct)
        {
            EnqueuedBackupJobIds.Add(backupJobId);
            return Task.CompletedTask;
        }

        public Task<BackupOffsiteConnectionStatus> GetConnectionStatusAsync(CancellationToken ct) =>
            Task.FromResult(new BackupOffsiteConnectionStatus(true, "google_drive", "drive-folder-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));

        public Task<PagedResult<BackupOffsiteUploadDto>> ListUploadsAsync(int page, int pageSize, CancellationToken ct) =>
            Task.FromResult(new PagedResult<BackupOffsiteUploadDto>(Array.Empty<BackupOffsiteUploadDto>(), page, pageSize, 0));

        public Task<bool> ProcessNextPendingUploadAsync(CancellationToken ct) => Task.FromResult(false);

        public Task<BackupOffsiteUploadDto> ReuploadAsync(Guid backupJobId, CancellationToken ct) =>
            Task.FromResult(new BackupOffsiteUploadDto(Guid.NewGuid(), backupJobId, "google_drive", "queued", null, null, null, 0, null, DateTimeOffset.UtcNow, null, null));

        public Task<BackupOffsiteUploadDto> TestUploadAsync(CancellationToken ct) =>
            Task.FromResult(new BackupOffsiteUploadDto(Guid.NewGuid(), null, "google_drive", "success", "file-id", "checksum", 1024, 0, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? Username => "test";
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
