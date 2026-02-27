namespace CongNoGolden.Application.Reports;

public interface IReportScheduleService
{
    Task<IReadOnlyList<ReportDeliveryScheduleItem>> ListAsync(CancellationToken ct);
    Task<ReportDeliveryScheduleItem> CreateAsync(ReportDeliveryScheduleUpsertRequest request, CancellationToken ct);
    Task<ReportDeliveryScheduleItem> UpdateAsync(Guid id, ReportDeliveryScheduleUpsertRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<ReportDeliveryRunItem> RunNowAsync(Guid id, CancellationToken ct);
    Task<PagedResult<ReportDeliveryRunItem>> ListRunsAsync(
        Guid scheduleId,
        ReportDeliveryRunListRequest request,
        CancellationToken ct);
    Task<int> RunDueSchedulesAsync(int batchSize, CancellationToken ct);
}
