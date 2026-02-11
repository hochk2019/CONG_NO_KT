namespace CongNoGolden.Application.Reports;

public sealed record ReportStatementPagedResult(
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<ReportStatementLine> Lines,
    int Page,
    int PageSize,
    int Total
);
