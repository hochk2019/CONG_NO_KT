using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class BackupJobLockTests
{
    private readonly TestDatabaseFixture _fixture;

    public BackupJobLockTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessJobAsync_WhenLockHeld_MarksJobSkipped()
    {
        await using var db = _fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE congno.backup_jobs, congno.backup_settings RESTART IDENTITY CASCADE;");

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            Type = "scheduled",
            Status = "queued",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.BackupJobs.Add(job);
        await db.SaveChangesAsync();

        await using var lockConn = new NpgsqlConnection(_fixture.ConnectionString);
        await lockConn.OpenAsync();
        await using (var lockCmd = new NpgsqlCommand(
            "SELECT pg_try_advisory_lock(hashtext('backup_job'))", lockConn))
        {
            var acquired = (bool)(await lockCmd.ExecuteScalarAsync())!;
            Assert.True(acquired);
        }

        var service = new BackupService(
            db,
            new TestCurrentUser(),
            new TestMaintenanceState(),
            new BackupQueue(),
            new BackupProcessRunner(),
            NullLogger<BackupService>.Instance,
            new ConfigurationBuilder().Build());

        await service.ProcessJobAsync(job.Id, CancellationToken.None);

        var refreshed = await db.BackupJobs.AsNoTracking().FirstAsync(j => j.Id == job.Id);
        Assert.Equal("skipped", refreshed.Status);
        Assert.NotNull(refreshed.FinishedAt);
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
