namespace CongNoGolden.Application.Reports;

public sealed record UpdateReportPreferencesRequest(
    IReadOnlyList<string>? KpiOrder,
    int? DueSoonDays
);
