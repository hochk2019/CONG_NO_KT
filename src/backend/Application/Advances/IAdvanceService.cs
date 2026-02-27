using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Advances;

public interface IAdvanceService
{
    Task<AdvanceDto> CreateAsync(AdvanceCreateRequest request, CancellationToken ct);
    Task<AdvanceDto> ApproveAsync(Guid advanceId, AdvanceApproveRequest request, CancellationToken ct);
    Task<AdvanceDto> VoidAsync(Guid advanceId, AdvanceVoidRequest request, CancellationToken ct);
    Task<AdvanceDto> UnvoidAsync(Guid advanceId, AdvanceUnvoidRequest request, CancellationToken ct);
    Task<AdvanceUpdateResult> UpdateAsync(Guid advanceId, AdvanceUpdateRequest request, CancellationToken ct);
    Task<PagedResult<AdvanceListItem>> ListAsync(AdvanceListRequest request, CancellationToken ct);
}
