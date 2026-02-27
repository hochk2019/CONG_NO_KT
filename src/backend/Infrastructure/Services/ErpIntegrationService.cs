using System.Net.Http.Json;
using CongNoGolden.Application.Integrations;
using CongNoGolden.Application.Reports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ErpIntegrationService : IErpIntegrationService
{
    private static readonly object SyncStateLock = new();
    private static ErpSyncState? _lastSyncState;

    private readonly HttpClient _httpClient;
    private readonly ConGNoDbContext _db;
    private readonly IOptions<ErpIntegrationOptions> _options;
    private readonly IReportService _reportService;
    private readonly ILogger<ErpIntegrationService> _logger;

    public ErpIntegrationService(
        HttpClient httpClient,
        ConGNoDbContext db,
        IOptions<ErpIntegrationOptions> options,
        IReportService reportService,
        ILogger<ErpIntegrationService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _options = options;
        _reportService = reportService;
        _logger = logger;
    }

    public async Task<ErpIntegrationConfig> GetConfigAsync(CancellationToken ct)
    {
        var options = await ResolveOptionsAsync(ct);
        var setting = await GetLatestSettingAsync(ct);
        return CreateConfig(options, setting?.UpdatedAt, setting?.UpdatedBy);
    }

    public async Task<ErpIntegrationConfig> UpdateConfigAsync(
        ErpIntegrationConfigUpdateRequest request,
        string? updatedBy,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var provider = string.IsNullOrWhiteSpace(request.Provider)
            ? "MISA"
            : request.Provider.Trim();
        var baseUrl = NormalizeNullable(request.BaseUrl);
        var companyCode = NormalizeNullable(request.CompanyCode);
        var timeoutSeconds = request.TimeoutSeconds;

        if (provider.Length > 32)
        {
            throw new InvalidOperationException("Provider không được vượt quá 32 ký tự.");
        }

        if (timeoutSeconds is < 5 or > 120)
        {
            throw new InvalidOperationException("TimeoutSeconds phải nằm trong khoảng 5-120.");
        }

        if (!string.IsNullOrWhiteSpace(baseUrl)
            && !Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("BaseUrl phải là URL tuyệt đối hợp lệ.");
        }

        var existing = await _db.ErpIntegrationSettings
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        var currentApiKey = ResolveCurrentApiKey(existing);
        var nextApiKey = request.ClearApiKey
            ? null
            : !string.IsNullOrWhiteSpace(request.ApiKey)
                ? request.ApiKey.Trim()
                : currentApiKey;

        if (request.Enabled
            && (string.IsNullOrWhiteSpace(baseUrl)
                || string.IsNullOrWhiteSpace(companyCode)
                || string.IsNullOrWhiteSpace(nextApiKey)))
        {
            throw new InvalidOperationException(
                "Để bật tích hợp ERP cần cấu hình đầy đủ Base URL, CompanyCode và API key.");
        }

        var now = DateTimeOffset.UtcNow;
        var normalizedUpdatedBy = NormalizeNullable(updatedBy);

        var setting = existing ?? new ErpIntegrationSetting
        {
            Id = Guid.NewGuid(),
            CreatedAt = now
        };

        setting.Enabled = request.Enabled;
        setting.Provider = provider;
        setting.BaseUrl = baseUrl;
        setting.CompanyCode = companyCode;
        setting.ApiKey = nextApiKey;
        setting.TimeoutSeconds = timeoutSeconds;
        setting.UpdatedBy = normalizedUpdatedBy;
        setting.UpdatedAt = now;

        if (existing is null)
        {
            _db.ErpIntegrationSettings.Add(setting);
        }

        await _db.SaveChangesAsync(ct);

        var options = NormalizeOptions(ToOptions(setting));
        return CreateConfig(options, setting.UpdatedAt, setting.UpdatedBy);
    }

    public async Task<ErpIntegrationStatus> GetStatusAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var options = await ResolveOptionsAsync(ct);
        var state = ReadSyncState();

        return new ErpIntegrationStatus(
            options.Provider,
            options.Enabled,
            options.Configured,
            options.HasApiKey,
            options.BaseUrl,
            options.CompanyCode,
            options.TimeoutSeconds,
            state?.ExecutedAtUtc,
            state?.Status,
            state?.Message);
    }

    public async Task<ErpSyncSummaryResult> SyncSummaryAsync(ErpSyncSummaryRequest request, CancellationToken ct)
    {
        var normalizedRequest = request with
        {
            DueSoonDays = Math.Clamp(request.DueSoonDays, 1, 60)
        };

        var payload = await BuildSummaryPayloadAsync(normalizedRequest, ct);
        var options = await ResolveOptionsAsync(ct);

        if (!options.Enabled)
        {
            return RememberResult(
                success: false,
                status: "disabled",
                message: "Tích hợp ERP đang tắt trong cấu hình.",
                provider: options.Provider,
                requestId: null,
                payload: payload);
        }

        if (!options.Configured)
        {
            return RememberResult(
                success: false,
                status: "not_configured",
                message: "Thiếu cấu hình ERP (baseUrl/apiKey/companyCode).",
                provider: options.Provider,
                requestId: null,
                payload: payload);
        }

        if (normalizedRequest.DryRun)
        {
            return RememberResult(
                success: true,
                status: "dry_run",
                message: "Đã kiểm tra payload đồng bộ (dry-run).",
                provider: options.Provider,
                requestId: null,
                payload: payload);
        }

        var requestId = Guid.NewGuid().ToString("N");
        var endpoint = $"{options.BaseUrl!.TrimEnd('/')}/sync/summary";
        var envelope = new ErpSyncEnvelope(
            SourceSystem: "congno-golden",
            Provider: options.Provider,
            CompanyCode: options.CompanyCode!,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            RequestedBy: normalizedRequest.RequestedBy,
            Summary: payload);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Headers.Add("X-Api-Key", options.ApiKey!);
            httpRequest.Headers.Add("X-Company-Code", options.CompanyCode!);
            httpRequest.Headers.Add("X-Request-Id", requestId);
            httpRequest.Content = JsonContent.Create(envelope);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

            using var response = await _httpClient.SendAsync(httpRequest, timeoutCts.Token);
            if (response.IsSuccessStatusCode)
            {
                return RememberResult(
                    success: true,
                    status: "success",
                    message: "Đồng bộ số liệu tổng hợp lên ERP thành công.",
                    provider: options.Provider,
                    requestId: requestId,
                    payload: payload);
            }

            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            _logger.LogWarning(
                "ERP sync failed with status {StatusCode}. Body: {Body}",
                (int)response.StatusCode,
                body);

            return RememberResult(
                success: false,
                status: "failed",
                message: $"ERP trả về lỗi {(int)response.StatusCode}.",
                provider: options.Provider,
                requestId: requestId,
                payload: payload);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "ERP sync request timed out.");
            return RememberResult(
                success: false,
                status: "timeout",
                message: "Đồng bộ ERP quá thời gian chờ.",
                provider: options.Provider,
                requestId: requestId,
                payload: payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERP sync request failed.");
            return RememberResult(
                success: false,
                status: "error",
                message: "Không thể gửi yêu cầu đồng bộ ERP.",
                provider: options.Provider,
                requestId: requestId,
                payload: payload);
        }
    }

    private async Task<ErpSyncSummaryPayload> BuildSummaryPayloadAsync(
        ErpSyncSummaryRequest request,
        CancellationToken ct)
    {
        var kpis = await _reportService.GetKpisAsync(
            new ReportKpiRequest(
                request.From,
                request.To,
                request.AsOfDate,
                SellerTaxCode: null,
                CustomerTaxCode: null,
                OwnerId: null,
                request.DueSoonDays),
            ct);

        return new ErpSyncSummaryPayload(
            request.From,
            request.To,
            request.AsOfDate,
            request.DueSoonDays,
            kpis.TotalOutstanding,
            kpis.OutstandingInvoice,
            kpis.OutstandingAdvance,
            kpis.UnallocatedReceiptsAmount,
            kpis.UnallocatedReceiptsCount,
            kpis.OverdueAmount,
            kpis.OverdueCustomers,
            kpis.DueSoonAmount,
            kpis.DueSoonCustomers,
            kpis.OnTimeCustomers);
    }

    private static ErpIntegrationConfig CreateConfig(
        NormalizedErpOptions options,
        DateTimeOffset? updatedAtUtc,
        string? updatedBy)
    {
        return new ErpIntegrationConfig(
            options.Enabled,
            options.Provider,
            options.BaseUrl,
            options.CompanyCode,
            options.TimeoutSeconds,
            options.HasApiKey,
            updatedAtUtc,
            updatedBy);
    }

    private async Task<NormalizedErpOptions> ResolveOptionsAsync(CancellationToken ct)
    {
        var setting = await GetLatestSettingAsync(ct);
        if (setting is null)
        {
            return NormalizeOptions(_options.Value);
        }

        return NormalizeOptions(ToOptions(setting));
    }

    private async Task<ErpIntegrationSetting?> GetLatestSettingAsync(CancellationToken ct)
    {
        return await _db.ErpIntegrationSettings
            .AsNoTracking()
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private string? ResolveCurrentApiKey(ErpIntegrationSetting? setting)
    {
        if (!string.IsNullOrWhiteSpace(setting?.ApiKey))
        {
            return setting.ApiKey!.Trim();
        }

        return NormalizeNullable(_options.Value.ApiKey);
    }

    private static ErpIntegrationOptions ToOptions(ErpIntegrationSetting setting)
    {
        return new ErpIntegrationOptions
        {
            Enabled = setting.Enabled,
            Provider = setting.Provider,
            BaseUrl = setting.BaseUrl ?? string.Empty,
            ApiKey = setting.ApiKey ?? string.Empty,
            CompanyCode = setting.CompanyCode ?? string.Empty,
            TimeoutSeconds = setting.TimeoutSeconds
        };
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static NormalizedErpOptions NormalizeOptions(ErpIntegrationOptions options)
    {
        var provider = string.IsNullOrWhiteSpace(options.Provider) ? "MISA" : options.Provider.Trim();
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? null : options.BaseUrl.Trim();
        var apiKey = string.IsNullOrWhiteSpace(options.ApiKey) ? null : options.ApiKey.Trim();
        var companyCode = string.IsNullOrWhiteSpace(options.CompanyCode) ? null : options.CompanyCode.Trim();
        var timeoutSeconds = Math.Clamp(options.TimeoutSeconds, 5, 120);

        return new NormalizedErpOptions(
            Provider: provider,
            Enabled: options.Enabled,
            Configured: !string.IsNullOrWhiteSpace(baseUrl)
                && !string.IsNullOrWhiteSpace(apiKey)
                && !string.IsNullOrWhiteSpace(companyCode),
            HasApiKey: !string.IsNullOrWhiteSpace(apiKey),
            BaseUrl: baseUrl,
            ApiKey: apiKey,
            CompanyCode: companyCode,
            TimeoutSeconds: timeoutSeconds);
    }

    private static ErpSyncState? ReadSyncState()
    {
        lock (SyncStateLock)
        {
            return _lastSyncState;
        }
    }

    private static ErpSyncSummaryResult RememberResult(
        bool success,
        string status,
        string message,
        string provider,
        string? requestId,
        ErpSyncSummaryPayload payload)
    {
        var executedAtUtc = DateTimeOffset.UtcNow;
        lock (SyncStateLock)
        {
            _lastSyncState = new ErpSyncState(executedAtUtc, status, message);
        }

        return new ErpSyncSummaryResult(
            success,
            status,
            message,
            executedAtUtc,
            provider,
            requestId,
            payload);
    }

    private sealed record ErpSyncEnvelope(
        string SourceSystem,
        string Provider,
        string CompanyCode,
        DateTimeOffset GeneratedAtUtc,
        string? RequestedBy,
        ErpSyncSummaryPayload Summary);

    private sealed record ErpSyncState(
        DateTimeOffset ExecutedAtUtc,
        string Status,
        string Message);

    private sealed record NormalizedErpOptions(
        string Provider,
        bool Enabled,
        bool Configured,
        bool HasApiKey,
        string? BaseUrl,
        string? ApiKey,
        string? CompanyCode,
        int TimeoutSeconds);
}
