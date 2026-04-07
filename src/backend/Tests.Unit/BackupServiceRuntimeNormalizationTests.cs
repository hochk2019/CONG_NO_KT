using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupServiceRuntimeNormalizationTests
{
    [Fact]
    public async Task GetSettingsAsync_NormalizesWindowsPaths_OnNonWindowsRuntime()
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"backup_runtime_normalize_{Guid.NewGuid():N}")
            .Options;

        await using var db = new ConGNoDbContext(options);
        db.BackupSettings.Add(new BackupSettings
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            BackupPath = @"C:\apps\congno\backup\dumps",
            RetentionCount = 10,
            ScheduleDayOfWeek = 1,
            ScheduleTime = "02:00",
            PgBinPath = @"C:\Program Files\PostgreSQL\16\bin",
            Timezone = "UTC",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        const string configuredBackupPath = "/var/lib/congno/backups/dumps";
        const string configuredPgBinPath = "/usr/bin";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backup:DefaultPath"] = configuredBackupPath,
                ["Backup:DefaultPgBinPath"] = configuredPgBinPath
            })
            .Build();

        var service = new BackupService(
            db,
            new TestCurrentUser(),
            new TestMaintenanceState(),
            new BackupQueue(),
            new BackupProcessRunner(),
            NullLogger<BackupService>.Instance,
            configuration);

        var settings = await service.GetSettingsAsync(CancellationToken.None);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(@"C:\apps\congno\backup\dumps", settings.BackupPath);
            Assert.Equal(@"C:\Program Files\PostgreSQL\16\bin", settings.PgBinPath);
            return;
        }

        Assert.Equal(configuredBackupPath, settings.BackupPath);
        Assert.Equal(configuredPgBinPath, settings.PgBinPath);
    }

    [Fact]
    public async Task GetSettingsAsync_ExposesHostPathHint_WhenRunningInContainer()
    {
        var previous = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");

        try
        {
            var options = new DbContextOptionsBuilder<ConGNoDbContext>()
                .UseInMemoryDatabase($"backup_runtime_container_{Guid.NewGuid():N}")
                .Options;

            await using var db = new ConGNoDbContext(options);
            db.BackupSettings.Add(new BackupSettings
            {
                Id = Guid.NewGuid(),
                Enabled = true,
                BackupPath = "/var/lib/congno/backups/dumps",
                RetentionCount = 10,
                ScheduleDayOfWeek = 1,
                ScheduleTime = "02:00",
                PgBinPath = "/usr/bin",
                Timezone = "UTC",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();

            const string hostBackupPath = "C:/Backup/CongNo";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Backup:DefaultPath"] = "/var/lib/congno/backups/dumps",
                    ["Backup:DefaultPgBinPath"] = "/usr/bin",
                    ["Backup:HostPathHint"] = hostBackupPath
                })
                .Build();

            var service = new BackupService(
                db,
                new TestCurrentUser(),
                new TestMaintenanceState(),
                new BackupQueue(),
                new BackupProcessRunner(),
                NullLogger<BackupService>.Instance,
                configuration);

            var settings = await service.GetSettingsAsync(CancellationToken.None);

            Assert.True(settings.UsesContainerPaths);
            Assert.Equal(hostBackupPath, settings.HostBackupPath);
            Assert.Equal("BACKUP_HOST_PATH", settings.HostBackupPathConfigKey);
            Assert.False(settings.CanEditBackupPath);
            Assert.False(settings.CanEditPgBinPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", previous);
        }
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
