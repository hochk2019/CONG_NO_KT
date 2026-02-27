using CongNoGolden.Api;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Integrations;
using CongNoGolden.Application.Reports;

namespace CongNoGolden.Api.Endpoints;

public static class ErpIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapErpIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/erp-integration/config", async (
            IErpIntegrationService erpIntegrationService,
            CancellationToken ct) =>
        {
            var config = await erpIntegrationService.GetConfigAsync(ct);
            return Results.Ok(config);
        })
        .WithName("AdminErpIntegrationConfig")
        .WithTags("Admin")
        .RequireAuthorization("AdminHealthView");

        app.MapPut("/admin/erp-integration/config", async (
            ErpIntegrationConfigUpdateRequest request,
            IErpIntegrationService erpIntegrationService,
            ICurrentUser currentUser,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            try
            {
                var before = await erpIntegrationService.GetConfigAsync(ct);
                var updated = await erpIntegrationService.UpdateConfigAsync(request, currentUser.Username, ct);

                await auditService.LogAsync(
                    "ERP_CONFIG_UPDATE",
                    "Maintenance",
                    "erp-integration-config",
                    new
                    {
                        before.Enabled,
                        before.Provider,
                        before.BaseUrl,
                        before.CompanyCode,
                        before.TimeoutSeconds,
                        before.HasApiKey
                    },
                    new
                    {
                        updated.Enabled,
                        updated.Provider,
                        updated.BaseUrl,
                        updated.CompanyCode,
                        updated.TimeoutSeconds,
                        updated.HasApiKey,
                        updated.UpdatedAtUtc,
                        updated.UpdatedBy
                    },
                    ct);

                return Results.Ok(updated);
            }
            catch (Exception ex)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("AdminErpIntegrationConfigUpdate")
        .WithTags("Admin")
        .RequireAuthorization("AdminHealthView");

        app.MapGet("/admin/erp-integration/status", async (
            IErpIntegrationService erpIntegrationService,
            CancellationToken ct) =>
        {
            var status = await erpIntegrationService.GetStatusAsync(ct);
            return Results.Ok(status);
        })
        .WithName("AdminErpIntegrationStatus")
        .WithTags("Admin")
        .RequireAuthorization("AdminHealthView");

        app.MapPost("/admin/erp-integration/sync-summary", async (
            AdminErpSyncSummaryRequest? request,
            IErpIntegrationService erpIntegrationService,
            ICurrentUser currentUser,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var payload = request ?? new AdminErpSyncSummaryRequest(
                From: null,
                To: null,
                AsOfDate: null,
                DueSoonDays: null,
                DryRun: null);

            var rangeError = ReportRequestValidator.ValidateDateRange(payload.From, payload.To);
            if (rangeError is not null)
            {
                return ApiErrors.InvalidRequest(rangeError);
            }

            var dueSoonDays = payload.DueSoonDays.GetValueOrDefault(7);
            if (dueSoonDays is < 1 or > 60)
            {
                return ApiErrors.InvalidRequest("DueSoonDays phải nằm trong khoảng 1-60.");
            }

            var syncResult = await erpIntegrationService.SyncSummaryAsync(
                new ErpSyncSummaryRequest(
                    payload.From,
                    payload.To,
                    payload.AsOfDate,
                    dueSoonDays,
                    payload.DryRun ?? false,
                    currentUser.Username),
                ct);

            await auditService.LogAsync(
                "ERP_SYNC_SUMMARY",
                "Maintenance",
                "erp-integration",
                payload,
                new
                {
                    syncResult.Success,
                    syncResult.Status,
                    syncResult.Message,
                    syncResult.Provider,
                    syncResult.RequestId,
                    syncResult.ExecutedAtUtc
                },
                ct);

            return Results.Ok(syncResult);
        })
        .WithName("AdminErpIntegrationSyncSummary")
        .WithTags("Admin")
        .RequireAuthorization("AdminHealthView");

        return app;
    }
}

public sealed record AdminErpSyncSummaryRequest(
    DateOnly? From,
    DateOnly? To,
    DateOnly? AsOfDate,
    int? DueSoonDays,
    bool? DryRun);
