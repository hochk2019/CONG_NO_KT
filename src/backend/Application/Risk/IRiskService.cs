using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Risk;

public interface IRiskService
{
    Task<RiskOverviewDto> GetOverviewAsync(RiskOverviewRequest request, CancellationToken ct);
    Task<PagedResult<RiskCustomerItem>> ListCustomersAsync(RiskCustomerListRequest request, CancellationToken ct);
    Task<IReadOnlyList<RiskRuleDto>> GetRulesAsync(CancellationToken ct);
    Task UpdateRulesAsync(RiskRulesUpdateRequest request, CancellationToken ct);
}
