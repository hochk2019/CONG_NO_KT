namespace CongNoGolden.Application.Integrations;

public interface IErpIntegrationService
{
    Task<ErpIntegrationConfig> GetConfigAsync(CancellationToken ct);

    Task<ErpIntegrationConfig> UpdateConfigAsync(
        ErpIntegrationConfigUpdateRequest request,
        string? updatedBy,
        CancellationToken ct);

    Task<ErpIntegrationStatus> GetStatusAsync(CancellationToken ct);

    Task<ErpSyncSummaryResult> SyncSummaryAsync(ErpSyncSummaryRequest request, CancellationToken ct);
}
