namespace CongNoGolden.Application.Reports;

public sealed record ReportPreferencesDto(
    IReadOnlyList<string> KpiOrder,
    int DueSoonDays
);
