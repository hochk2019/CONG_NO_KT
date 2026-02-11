namespace CongNoGolden.Application.Imports;

public sealed record ImportPreviewResult(
    int TotalRows,
    int OkCount,
    int WarnCount,
    int ErrorCount,
    int Page,
    int PageSize,
    IReadOnlyList<ImportPreviewRow> Rows
);
