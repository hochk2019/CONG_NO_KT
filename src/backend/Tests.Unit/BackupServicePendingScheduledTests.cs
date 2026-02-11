using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupServicePendingScheduledTests
{
    [Fact]
    public async Task HasPendingScheduledBackupAsync_ReturnsTrue_WhenQueuedOrRunningExists()
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"backup_pending_scheduled_{Guid.NewGuid():N}")
            .Options;

        await using var db = new ConGNoDbContext(options);
        db.BackupJobs.Add(new BackupJob
        {
            Id = Guid.NewGuid(),
            Type = "scheduled",
            Status = "queued",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new BackupService(
            db,
            new TestCurrentUser(),
            new TestMaintenanceState(),
            new BackupQueue(),
            new BackupProcessRunner(),
            NullLogger<BackupService>.Instance,
            new ConfigurationBuilder().Build());

        var hasPending = await service.HasPendingScheduledBackupAsync(CancellationToken.None);

        Assert.True(hasPending);
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
