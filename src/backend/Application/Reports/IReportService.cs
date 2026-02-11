namespace CongNoGolden.Application.Reports;

public interface IReportService
{
    Task<IReadOnlyList<ReportSummaryRow>> GetSummaryAsync(ReportSummaryRequest request, CancellationToken ct);
    Task<PagedResult<ReportSummaryRow>> GetSummaryPagedAsync(ReportSummaryPagedRequest request, CancellationToken ct);
    Task<ReportStatementResult> GetStatementAsync(ReportStatementRequest request, CancellationToken ct);
    Task<ReportStatementPagedResult> GetStatementPagedAsync(ReportStatementPagedRequest request, CancellationToken ct);
    Task<IReadOnlyList<ReportAgingRow>> GetAgingAsync(ReportAgingRequest request, CancellationToken ct);
    Task<PagedResult<ReportAgingRow>> GetAgingPagedAsync(ReportAgingPagedRequest request, CancellationToken ct);
    Task<ReportKpiDto> GetKpisAsync(ReportKpiRequest request, CancellationToken ct);
    Task<ReportChartsDto> GetChartsAsync(ReportChartsRequest request, CancellationToken ct);
    Task<ReportInsightsDto> GetInsightsAsync(ReportInsightsRequest request, CancellationToken ct);
    Task<ReportPreferencesDto> GetPreferencesAsync(Guid userId, CancellationToken ct);
    Task<ReportPreferencesDto> UpdatePreferencesAsync(
        Guid userId,
        UpdateReportPreferencesRequest request,
        CancellationToken ct);
}
