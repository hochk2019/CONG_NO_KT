using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Risk;

public interface IRiskService
{
    Task<RiskOverviewDto> GetOverviewAsync(RiskOverviewRequest request, CancellationToken ct);
    Task<PagedResult<RiskCustomerItem>> ListCustomersAsync(RiskCustomerListRequest request, CancellationToken ct);
    Task<PagedResult<RiskDeltaAlertItem>> ListDeltaAlertsAsync(RiskDeltaAlertListRequest request, CancellationToken ct);
    Task<IReadOnlyList<RiskScoreHistoryPoint>> GetScoreHistoryAsync(
        string customerTaxCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        int take,
        CancellationToken ct);
    Task<IReadOnlyList<RiskRuleDto>> GetRulesAsync(CancellationToken ct);
    Task UpdateRulesAsync(RiskRulesUpdateRequest request, CancellationToken ct);
    Task<RiskSnapshotCaptureResult> CaptureRiskSnapshotsAsync(
        DateOnly asOfDate,
        decimal absoluteThreshold,
        decimal relativeThresholdRatio,
        CancellationToken ct);
}
