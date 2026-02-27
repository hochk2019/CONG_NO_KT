using CongNoGolden.Application.Maintenance;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Infrastructure.Services;

public sealed class DataRetentionOptions
{
    public bool AutoRunEnabled { get; set; } = true;
    public int PollMinutes { get; set; } = 1440;
    public int AuditLogRetentionDays { get; set; } = 365;
    public int ImportStagingRetentionDays { get; set; } = 90;
    public int RefreshTokenRetentionDays { get; set; } = 30;
    public int DeleteBatchSize { get; set; } = 1000;
}

public sealed class DataRetentionService : IDataRetentionService
{
    private static readonly HashSet<string> TerminalImportStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "COMMITTED",
        "ROLLED_BACK",
        "CANCELLED"
    };

    private readonly ConGNoDbContext _db;
    private readonly DataRetentionOptions _options;
    private readonly ILogger<DataRetentionService> _logger;

    public DataRetentionService(
        ConGNoDbContext db,
        IOptions<DataRetentionOptions> options,
        ILogger<DataRetentionService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DataRetentionRunResult> RunAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var auditCutoff = now.AddDays(-Math.Max(1, _options.AuditLogRetentionDays));
        var stagingCutoff = now.AddDays(-Math.Max(1, _options.ImportStagingRetentionDays));
        var refreshCutoff = now.AddDays(-Math.Max(1, _options.RefreshTokenRetentionDays));
        var deleteBatchSize = Math.Max(1, _options.DeleteBatchSize);
        await EnsureAuditLogPartitionsAsync(ct);

        var deletedAuditLogs = await DeleteAuditLogsAsync(auditCutoff, deleteBatchSize, ct);
        var deletedStagingRows = await DeleteImportStagingRowsAsync(stagingCutoff, deleteBatchSize, ct);
        var deletedRefreshTokens = await DeleteRefreshTokensAsync(refreshCutoff, deleteBatchSize, ct);

        _logger.LogInformation(
            "Data retention run completed. Deleted audit={AuditDeleted}, importStaging={StagingDeleted}, refreshTokens={RefreshDeleted}",
            deletedAuditLogs,
            deletedStagingRows,
            deletedRefreshTokens);

        return new DataRetentionRunResult(
            now,
            deletedAuditLogs,
            deletedStagingRows,
            deletedRefreshTokens);
    }

    private async Task<int> DeleteAuditLogsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        return await DeleteInBatchesAsync(
            _db.AuditLogs
                .Where(x => x.CreatedAt < cutoff)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Select(x => x.Id),
            ids => _db.AuditLogs.Where(x => ids.Contains(x.Id)),
            batchSize,
            ct);
    }

    private async Task<int> DeleteImportStagingRowsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        var terminalBatchIds = _db.ImportBatches
            .Where(x => TerminalImportStatuses.Contains(x.Status))
            .Select(x => x.Id);

        return await DeleteInBatchesAsync(
            _db.ImportStagingRows
                .Where(x => x.CreatedAt < cutoff && terminalBatchIds.Contains(x.BatchId))
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Select(x => x.Id),
            ids => _db.ImportStagingRows.Where(x => ids.Contains(x.Id)),
            batchSize,
            ct);
    }

    private async Task<int> DeleteRefreshTokensAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        return await DeleteInBatchesAsync(
            _db.RefreshTokens
                .Where(rt =>
                    (rt.RevokedAt.HasValue && rt.RevokedAt.Value < cutoff) ||
                    rt.AbsoluteExpiresAt < cutoff)
                .OrderBy(rt => rt.AbsoluteExpiresAt)
                .ThenBy(rt => rt.Id)
                .Select(rt => rt.Id),
            ids => _db.RefreshTokens.Where(x => ids.Contains(x.Id)),
            batchSize,
            ct);
    }

    private async Task<int> DeleteInBatchesAsync<TEntity>(
        IQueryable<Guid> keyQuery,
        Func<IReadOnlyCollection<Guid>, IQueryable<TEntity>> targetFactory,
        int batchSize,
        CancellationToken ct)
        where TEntity : class
    {
        var totalDeleted = 0;
        while (true)
        {
            var keys = await keyQuery.Take(batchSize).ToListAsync(ct);
            if (keys.Count == 0)
            {
                break;
            }

            if (_db.Database.IsRelational())
            {
                var deleted = await targetFactory(keys).ExecuteDeleteAsync(ct);
                if (deleted <= 0)
                {
                    break;
                }

                totalDeleted += deleted;
                continue;
            }

            var entities = await targetFactory(keys).ToListAsync(ct);
            if (entities.Count == 0)
            {
                break;
            }

            _db.RemoveRange(entities);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            totalDeleted += entities.Count;
        }

        return totalDeleted;
    }

    private async Task EnsureAuditLogPartitionsAsync(CancellationToken ct)
    {
        if (!_db.Database.IsRelational())
        {
            return;
        }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT congno.ensure_audit_logs_partition(date_trunc('month', now())::date);",
                ct);

            await _db.Database.ExecuteSqlRawAsync(
                "SELECT congno.ensure_audit_logs_partition((date_trunc('month', now()) + INTERVAL '1 month')::date);",
                ct);

            await _db.Database.ExecuteSqlRawAsync(
                "SELECT congno.ensure_audit_logs_partition((date_trunc('month', now()) + INTERVAL '2 month')::date);",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to ensure audit log partitions. Data retention will continue with row-level deletes.");
        }
    }
}
