using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using Ops.Shared.Config;
using Ops.Shared.Models;

namespace Ops.Shared.Console;

public sealed class AgentClient
{
    private readonly Func<HttpMessageHandler> _handlerFactory;
    private HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AgentClient(Func<HttpMessageHandler>? handlerFactory = null)
    {
        _handlerFactory = handlerFactory ?? (() => new HttpClientHandler());
        _http = new HttpClient(_handlerFactory());
    }

    public void Configure(string baseUrl, string apiKey)
    {
        var normalized = AgentConnection.NormalizeBaseUrl(baseUrl);
        var next = new HttpClient(_handlerFactory()) { BaseAddress = new Uri(normalized) };
        next.DefaultRequestHeaders.Remove("X-Api-Key");
        if (!string.IsNullOrWhiteSpace(apiKey))
            next.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var previous = Interlocked.Exchange(ref _http, next);
        previous.Dispose();
    }

    public Task<object?> GetHealthAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<object>("health", ct);

    public Task<OpsConfig?> GetConfigAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<OpsConfig>("config", ct);

    public async Task<OpsConfig?> SaveConfigAsync(OpsConfig config, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync("config", config, ct);
        return await response.Content.ReadFromJsonAsync<OpsConfig>(ct);
    }

    public Task<StatusResponse?> GetStatusAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<StatusResponse>("status", ct);

    public Task<SystemMetricsDto?> GetSystemMetricsAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<SystemMetricsDto>("metrics/system", ct);

    public async Task<ServiceStatusDto?> StartBackendAsync(CancellationToken ct)
        => await PostForServiceStatusAsync("services/backend/start", ct);

    public async Task<ServiceStatusDto?> StopBackendAsync(CancellationToken ct)
        => await PostForServiceStatusAsync("services/backend/stop", ct);

    public async Task<ServiceStatusDto?> RestartBackendAsync(CancellationToken ct)
        => await PostForServiceStatusAsync("services/backend/restart", ct);

    public async Task<ServiceStatusDto?> StartFrontendAsync(CancellationToken ct)
        => await PostForServiceStatusAsync("services/frontend/start", ct);

    public async Task<ServiceStatusDto?> StopFrontendAsync(CancellationToken ct)
        => await PostForServiceStatusAsync("services/frontend/stop", ct);

    public Task<IisBindingDto[]?> GetFrontendBindingsAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<IisBindingDto[]>("frontend/bindings", ct);

    public async Task<CommandResponse?> SetFrontendBindingAsync(IisBindingUpdateRequest request, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync("frontend/bindings", request, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public Task<AppPoolStatusDto?> GetAppPoolStatusAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<AppPoolStatusDto>("frontend/app-pool/status", ct);

    public async Task<CommandResponse?> StartAppPoolAsync(CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("frontend/app-pool/start", new { }, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public async Task<CommandResponse?> StopAppPoolAsync(CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("frontend/app-pool/stop", new { }, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public async Task<CommandResponse?> RecycleAppPoolAsync(CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("frontend/app-pool/recycle", new { }, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public Task<CompressionSettingsDto?> GetCompressionSettingsAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<CompressionSettingsDto>("frontend/compression", ct);

    public async Task<CommandResponse?> SetCompressionSettingsAsync(CompressionSettingsDto request, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync("frontend/compression", request, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public async Task<CommandResponse?> ClearFrontendCacheAsync(CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("frontend/cache/clear", new { }, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public Task<MaintenanceModeDto?> GetFrontendMaintenanceAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<MaintenanceModeDto>("frontend/maintenance", ct);

    public async Task<CommandResponse?> SetFrontendMaintenanceAsync(MaintenanceModeRequest request, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync("frontend/maintenance", request, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public Task<ComponentVersionDto?> GetFrontendVersionAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<ComponentVersionDto>("frontend/version", ct);

    public async Task<string[]?> GetBackupsAsync(CancellationToken ct)
        => await _http.GetFromJsonAsync<string[]>("backups", ct);

    public async Task<BackupResponse?> CreateBackupAsync(CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("backup/create", new { }, ct);
        return await response.Content.ReadFromJsonAsync<BackupResponse>(ct);
    }

    public async Task<BackupResponse?> RestoreBackupAsync(string filePath, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("backup/restore", new { filePath }, ct);
        return await response.Content.ReadFromJsonAsync<BackupResponse>(ct);
    }

    public Task<BackupScheduleDto?> GetBackupScheduleAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<BackupScheduleDto>("backup/schedule", ct);

    public async Task<BackupScheduleDto?> UpdateBackupScheduleAsync(BackupScheduleDto request, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync("backup/schedule", request, ct);
        return await response.Content.ReadFromJsonAsync<BackupScheduleDto>(ct);
    }

    public async Task<BackupResponse?> RunBackupNowAsync(CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("backup/run-now", new { }, ct);
        return await response.Content.ReadFromJsonAsync<BackupResponse>(ct);
    }

    public Task<LogTailResponse?> GetLogTailAsync(string? path, int? lines, CancellationToken ct)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(path))
            query.Add($"path={Uri.EscapeDataString(path)}");
        if (lines is > 0)
            query.Add($"lines={lines}");

        var url = "logs/tail";
        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        return _http.GetFromJsonAsync<LogTailResponse>(url, ct);
    }

    public Task<Ops.Shared.Models.DiagnosticsResponse?> GetDiagnosticsAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<Ops.Shared.Models.DiagnosticsResponse>("diagnostics", ct);

    public Task<PrereqItemDto[]?> GetPrerequisitesAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<PrereqItemDto[]>("prereq", ct);

    public async Task<CommandResponse?> InstallPrerequisiteAsync(string id, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("prereq/install", new PrereqInstallRequest(id), ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public Task<ComponentVersionDto?> GetBackendVersionAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<ComponentVersionDto>("backend/version", ct);

    public Task<ServiceConfigDto?> GetBackendServiceConfigAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<ServiceConfigDto>("backend/service/config", ct);

    public async Task<CommandResponse?> UpdateBackendServiceConfigAsync(ServiceConfigUpdateRequest request, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync("backend/service/config", request, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public Task<ServiceRecoveryDto?> GetBackendServiceRecoveryAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<ServiceRecoveryDto>("backend/service/recovery", ct);

    public async Task<CommandResponse?> UpdateBackendServiceRecoveryAsync(ServiceRecoveryUpdateRequest request, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync("backend/service/recovery", request, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public Task<BackendLogLevelDto?> GetBackendLogLevelAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<BackendLogLevelDto>("backend/log-level", ct);

    public async Task<BackendLogLevelDto?> UpdateBackendLogLevelAsync(BackendLogLevelUpdateRequest request, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync("backend/log-level", request, ct);
        return await response.Content.ReadFromJsonAsync<BackendLogLevelDto>(ct);
    }

    public Task<BackendJobSettingsDto?> GetBackendJobsAsync(CancellationToken ct)
        => _http.GetFromJsonAsync<BackendJobSettingsDto>("backend/jobs", ct);

    public async Task<BackendJobSettingsDto?> UpdateBackendJobsAsync(BackendJobSettingsUpdateRequest request, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync("backend/jobs", request, ct);
        return await response.Content.ReadFromJsonAsync<BackendJobSettingsDto>(ct);
    }

    public async Task<SqlExecuteResponse?> PreviewSqlAsync(string sql, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("db/sql/preview", new SqlExecuteRequest(sql), ct);
        return await response.Content.ReadFromJsonAsync<SqlExecuteResponse>(ct);
    }

    public async Task<SqlExecuteResponse?> ExecuteSqlAsync(string sql, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("db/sql/execute", new SqlExecuteRequest(sql), ct);
        return await response.Content.ReadFromJsonAsync<SqlExecuteResponse>(ct);
    }

    public async Task<CommandResponse?> CreateDatabaseAsync(CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("db/create", new { }, ct);
        return await ReadCommandResponseAsync(response, ct, "Agent chưa cập nhật (thiếu /db/create)");
    }

    public async Task<CommandResponse?> RunMigrationsAsync(CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("db/migrate", new { }, ct);
        return await ReadCommandResponseAsync(response, ct, "Agent chưa cập nhật (thiếu /db/migrate)");
    }

    public async Task<CommandResponse?> InstallBackendServiceAsync(string? exePath, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("services/backend/install", new { exePath }, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public async Task<CommandResponse?> UpdateBackendAsync(string? sourcePath, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("update/backend", new { sourcePath }, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    public async Task<CommandResponse?> UpdateFrontendAsync(string? sourcePath, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("update/frontend", new { sourcePath }, ct);
        return await ReadCommandResponseAsync(response, ct);
    }

    private async Task<ServiceStatusDto?> PostForServiceStatusAsync(string path, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(path, new { }, ct);
        return await response.Content.ReadFromJsonAsync<ServiceStatusDto>(ct);
    }

    private static async Task<CommandResponse?> ReadCommandResponseAsync(
        HttpResponseMessage response,
        CancellationToken ct,
        string? notFoundHint = null)
    {
        var content = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            var message = string.IsNullOrWhiteSpace(content)
                ? response.ReasonPhrase ?? "Lỗi không xác định"
                : content;

            if (response.StatusCode == HttpStatusCode.NotFound && !string.IsNullOrWhiteSpace(notFoundHint))
                message = notFoundHint;

            return new CommandResponse(1, string.Empty, $"HTTP {status}: {message}");
        }

        if (string.IsNullOrWhiteSpace(content))
            return new CommandResponse(1, string.Empty, "Phản hồi rỗng từ agent");

        try
        {
            return JsonSerializer.Deserialize<CommandResponse>(content, JsonOptions);
        }
        catch (Exception ex)
        {
            return new CommandResponse(1, string.Empty, $"Phản hồi không hợp lệ: {ex.Message}");
        }
    }
}

public sealed record StatusResponse(ServiceStatusDto Backend, ServiceStatusDto Frontend);

public sealed record BackupResponse(string File, int ExitCode, string Stdout, string Stderr);

public sealed record LogTailResponse(string Path, int Lines, string Content);

public sealed record CommandResponse(int ExitCode, string Stdout, string Stderr);
