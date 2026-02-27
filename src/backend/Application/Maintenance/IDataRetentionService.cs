namespace CongNoGolden.Application.Maintenance;

public interface IDataRetentionService
{
    Task<DataRetentionRunResult> RunAsync(CancellationToken ct);
}

public sealed record DataRetentionRunResult(
    DateTimeOffset ExecutedAtUtc,
    int DeletedAuditLogs,
    int DeletedImportStagingRows,
    int DeletedRefreshTokens);
