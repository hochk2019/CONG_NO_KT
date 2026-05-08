using System.Diagnostics;
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

public sealed class BackupServiceOffsiteSettingsTests
{
    [Fact]
    public async Task GetSettingsAsync_IncludesOffsiteConfigurationAndConnectionStatus()
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"backup_offsite_settings_{Guid.NewGuid():N}")
            .Options;

        await using var db = new ConGNoDbContext(options);
        var now = DateTimeOffset.UtcNow;

        db.BackupSettings.Add(new BackupSettings
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            BackupPath = Path.Combine(Path.GetTempPath(), "congno-backups"),
            RetentionCount = 10,
            ScheduleDayOfWeek = 1,
            ScheduleTime = "02:00",
            Timezone = "UTC",
            PgBinPath = OperatingSystem.IsWindows() ? @"C:\Program Files\PostgreSQL\16\bin" : "/usr/bin",
            OffsiteEnabled = true,
            OffsiteProvider = "google_drive",
            GoogleDriveFolderId = "drive-folder-001",
            OffsiteRetentionCount = 5,
            UploadAfterBackup = true,
            LastRunAt = now.AddHours(-2),
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
            ConnectedAt = now.AddHours(-3),
            LastValidatedAt = now.AddHours(-1),
            CreatedAt = now.AddHours(-3),
            UpdatedAt = now.AddHours(-1)
        });

        await db.SaveChangesAsync();

        var service = new BackupService(
            db,
            new TestCurrentUser(),
            new TestMaintenanceState(),
            new BackupQueue(),
            new FakeBackupProcessRunner(),
            new FakeBackupOffsiteService(),
            NullLogger<BackupService>.Instance,
            new ConfigurationBuilder().Build());

        var settings = await service.GetSettingsAsync(CancellationToken.None);

        Assert.True(settings.OffsiteEnabled);
        Assert.Equal("google_drive", settings.Provider);
        Assert.Equal("drive-folder-001", settings.GoogleDriveFolderId);
        Assert.Equal(5, settings.OffsiteRetentionCount);
        Assert.True(settings.UploadAfterBackup);
        Assert.NotNull(settings.OffsiteConnection);
        Assert.True(settings.OffsiteConnection!.IsConnected);
        Assert.Equal("google_drive", settings.OffsiteConnection.Provider);
        Assert.Equal("drive-folder-001", settings.OffsiteConnection.GoogleDriveFolderId);
    }

    private sealed class FakeBackupProcessRunner : IBackupProcessRunner
    {
        public Task<BackupProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken ct) =>
            Task.FromResult(new BackupProcessResult(0, string.Empty, string.Empty));
    }

    private sealed class FakeBackupOffsiteService : IBackupOffsiteService
    {
        public Task<string> BuildGoogleDriveConnectUrlAsync(string redirectUri, string? folderId, CancellationToken ct) =>
            Task.FromResult("https://example.test/connect");

        public Task<BackupOffsiteConnectionStatus> CompleteGoogleDriveCallbackAsync(string code, string redirectUri, string? folderId, CancellationToken ct) =>
            Task.FromResult(new BackupOffsiteConnectionStatus(true, "google_drive", folderId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));

        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public Task EnqueueUploadForBackupJobAsync(Guid backupJobId, CancellationToken ct) => Task.CompletedTask;

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
