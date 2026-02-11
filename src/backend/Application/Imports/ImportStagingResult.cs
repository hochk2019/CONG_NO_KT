namespace CongNoGolden.Application.Imports;

public sealed record ImportStagingResult(
    int TotalRows,
    int OkCount,
    int WarnCount,
    int ErrorCount
);
