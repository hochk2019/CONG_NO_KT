using System.Text.Json;
using System.Windows;
using Ops.Shared.Models;

namespace Ops.Console;

public partial class MainWindow
{
    private void OnRefreshOverview(object? sender, RoutedEventArgs e)
    {
        _ = RefreshOverviewAsync();
    }

    private async void OnHealth(object? sender, RoutedEventArgs e)
    {
        try
        {
            var health = await _client.GetHealthAsync(CancellationToken.None);
            TxtHealth.Text = JsonSerializer.Serialize(health, JsonOptions);
        }
        catch (Exception ex)
        {
            TxtHealth.Text = ex.Message;
        }
    }

    private async void OnStatus(object? sender, RoutedEventArgs e)
    {
        await LoadStatusAsync();
    }

    private async Task RefreshOverviewAsync()
    {
        await LoadStatusAsync();
        await LoadMetricsAsync();
        await LoadVersionsAsync();
        TxtLastRefresh.Text = $"Cập nhật: {DateTime.Now:HH:mm:ss}";
    }

    private async Task LoadStatusAsync()
    {
        try
        {
            var status = await _client.GetStatusAsync(CancellationToken.None);
            TxtStatus.Text = status is null ? string.Empty : JsonSerializer.Serialize(status, JsonOptions);
            ApplyStatusBadge(TxtBackendStatus, BadgeBackendStatus, status?.Backend?.Status);
            ApplyStatusBadge(TxtFrontendStatus, BadgeFrontendStatus, status?.Frontend?.Status);
            ApplyStatusBadge(TxtBackendServiceStatus, BadgeBackendServiceStatus, status?.Backend?.Status);
            ApplyStatusBadge(TxtFrontendServiceStatus, BadgeFrontendServiceStatus, status?.Frontend?.Status);
            SetConnectionState(true, "Đã kết nối");
            await LoadAppPoolStatusAsync();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = ex.Message;
            SetConnectionState(false, "Mất kết nối");
        }
    }

    private async Task LoadMetricsAsync()
    {
        try
        {
            var metrics = await _client.GetSystemMetricsAsync(CancellationToken.None);
            if (metrics is null)
                return;

            TxtCpu.Text = $"{metrics.CpuUsagePercent:0.##}%";
            TxtRam.Text = $"{metrics.MemoryUsedMb:0.##} / {metrics.MemoryTotalMb:0.##} MB";
            TxtDisk.Text = $"{metrics.DiskFreeGb:0.##} / {metrics.DiskTotalGb:0.##} GB";
        }
        catch (Exception ex)
        {
            TxtCpu.Text = "--";
            TxtRam.Text = ex.Message;
        }
    }

    private async Task LoadVersionsAsync()
    {
        try
        {
            var backend = await _client.GetBackendVersionAsync(CancellationToken.None);
            var frontend = await _client.GetFrontendVersionAsync(CancellationToken.None);
            TxtBackendVersion.Text = FormatVersion(backend);
            TxtFrontendVersion.Text = FormatVersion(frontend);
        }
        catch
        {
            TxtBackendVersion.Text = string.Empty;
            TxtFrontendVersion.Text = string.Empty;
        }
    }

    private async Task LoadAppPoolStatusAsync()
    {
        try
        {
            var status = await _client.GetAppPoolStatusAsync(CancellationToken.None);
            ApplyStatusBadge(TxtAppPoolStatus, BadgeAppPoolStatus, status?.Status);
            ApplyStatusBadge(TxtAppPoolStatusOverview, BadgeAppPoolStatusOverview, status?.Status);
        }
        catch (Exception ex)
        {
            TxtAppPoolStatus.Text = ex.Message;
            TxtAppPoolStatusOverview.Text = ex.Message;
        }
    }

    private static string FormatVersion(ComponentVersionDto? info)
    {
        if (info is null)
            return string.Empty;

        var version = string.IsNullOrWhiteSpace(info.Version) ? "unknown" : info.Version;
        var time = info.LastWriteTime?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
        return string.IsNullOrWhiteSpace(time) ? $"v{version}" : $"v{version} • {time}";
    }
}
