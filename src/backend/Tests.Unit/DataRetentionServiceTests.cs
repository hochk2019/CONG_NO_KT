using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests.Unit;

public sealed class DataRetentionServiceTests
{
    [Fact]
    public async Task RunAsync_DeletesOldData_WithExpectedGuards()
    {
        await using var db = CreateDbContext(nameof(RunAsync_DeletesOldData_WithExpectedGuards));
        var now = DateTimeOffset.UtcNow;

        db.AuditLogs.AddRange(
            new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "A1",
                EntityType = "Test",
                EntityId = "1",
                CreatedAt = now.AddDays(-400)
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "A2",
                EntityType = "Test",
                EntityId = "2",
                CreatedAt = now.AddDays(-20)
            });

        var committedBatchId = Guid.NewGuid();
        var stagingBatchId = Guid.NewGuid();

        db.ImportBatches.AddRange(
            new ImportBatch
            {
                Id = committedBatchId,
                Type = "INVOICE",
                Source = "FILE",
                Status = "COMMITTED",
                CreatedAt = now.AddDays(-120)
            },
            new ImportBatch
            {
                Id = stagingBatchId,
                Type = "INVOICE",
                Source = "FILE",
                Status = "STAGING",
                CreatedAt = now.AddDays(-120)
            });

        db.ImportStagingRows.AddRange(
            new ImportStagingRow
            {
                Id = Guid.NewGuid(),
                BatchId = committedBatchId,
                RowNo = 1,
                RawData = "{}",
                ValidationStatus = "OK",
                CreatedAt = now.AddDays(-120)
            },
            new ImportStagingRow
            {
                Id = Guid.NewGuid(),
                BatchId = stagingBatchId,
                RowNo = 1,
                RawData = "{}",
                ValidationStatus = "OK",
                CreatedAt = now.AddDays(-120)
            });

        db.RefreshTokens.AddRange(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                TokenHash = "hash-1",
                ExpiresAt = now.AddDays(-90),
                AbsoluteExpiresAt = now.AddDays(-90),
                CreatedAt = now.AddDays(-120),
                RevokedAt = now.AddDays(-90)
            },
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                TokenHash = "hash-2",
                ExpiresAt = now.AddDays(5),
                AbsoluteExpiresAt = now.AddDays(5),
                CreatedAt = now.AddDays(-1),
                RevokedAt = null
            });

        await db.SaveChangesAsync();

        var service = new DataRetentionService(
            db,
            Options.Create(new DataRetentionOptions
            {
                AuditLogRetentionDays = 365,
                ImportStagingRetentionDays = 90,
                RefreshTokenRetentionDays = 30
            }),
            NullLogger<DataRetentionService>.Instance);

        var result = await service.RunAsync(CancellationToken.None);

        Assert.Equal(1, result.DeletedAuditLogs);
        Assert.Equal(1, result.DeletedImportStagingRows);
        Assert.Equal(1, result.DeletedRefreshTokens);

        Assert.Equal(1, await db.AuditLogs.CountAsync());
        Assert.Equal(1, await db.ImportStagingRows.CountAsync());
        Assert.Equal(1, await db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task RunAsync_WithSmallBatchSize_UsesMultipleSaveChangesBatches()
    {
        var interceptor = new SaveChangesCounterInterceptor();
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"data-retention-batch-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new ConGNoDbContext(options);
        var now = DateTimeOffset.UtcNow;

        db.AuditLogs.AddRange(
            Enumerable.Range(1, 5).Select(i => new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = $"A{i}",
                EntityType = "Test",
                EntityId = i.ToString(),
                CreatedAt = now.AddDays(-400)
            }));

        await db.SaveChangesAsync();
        interceptor.Reset();

        var service = new DataRetentionService(
            db,
            Options.Create(new DataRetentionOptions
            {
                AuditLogRetentionDays = 365,
                ImportStagingRetentionDays = 90,
                RefreshTokenRetentionDays = 30,
                DeleteBatchSize = 2
            }),
            NullLogger<DataRetentionService>.Instance);

        var result = await service.RunAsync(CancellationToken.None);

        Assert.Equal(5, result.DeletedAuditLogs);
        Assert.Equal(0, result.DeletedImportStagingRows);
        Assert.Equal(0, result.DeletedRefreshTokens);
        Assert.True(
            interceptor.SaveChangesAsyncCalls >= 3,
            $"Expected batched delete to call SaveChangesAsync multiple times, actual={interceptor.SaveChangesAsyncCalls}.");
    }

    private static ConGNoDbContext CreateDbContext(string testName)
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"data-retention-{testName}")
            .Options;
        return new ConGNoDbContext(options);
    }

    private sealed class SaveChangesCounterInterceptor : SaveChangesInterceptor
    {
        public int SaveChangesAsyncCalls { get; private set; }

        public void Reset()
        {
            SaveChangesAsyncCalls = 0;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SaveChangesAsyncCalls++;
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
