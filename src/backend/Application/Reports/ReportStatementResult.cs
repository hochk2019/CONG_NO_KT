namespace CongNoGolden.Application.Reports;

public sealed record ReportStatementResult(
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<ReportStatementLine> Lines
);
