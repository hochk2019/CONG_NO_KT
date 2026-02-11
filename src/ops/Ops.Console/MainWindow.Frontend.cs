using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Ops.Shared.Config;
using Ops.Shared.Models;

namespace Ops.Console;

public partial class MainWindow
{
    private async Task LoadFrontendAdvancedAsync()
    {
        try
        {
            var config = await _client.GetConfigAsync(CancellationToken.None);
            if (config is not null)
            {
                TxtIisSiteName.Text = config.Frontend.IisSiteName;
                TxtAppPoolName.Text = config.Frontend.AppPoolName;
            }

            var maintenance = await _client.GetFrontendMaintenanceAsync(CancellationToken.None);
            if (maintenance is not null)
            {
                ChkMaintenanceEnabled.IsChecked = maintenance.Enabled;
                TxtMaintenanceMessage.Text = maintenance.Message ?? string.Empty;
            }

            var compression = await _client.GetCompressionSettingsAsync(CancellationToken.None);
            if (compression is not null)
                ChkCompressionEnabled.IsChecked = compression.StaticEnabled || compression.DynamicEnabled;
        }
        catch
        {
            // ignore load errors
        }
    }

    private async void OnSaveIisConfig(object? sender, RoutedEventArgs e)
    {
        try
        {
            var siteName = TxtIisSiteName.Text.Trim();
            var appPoolName = TxtAppPoolName.Text.Trim();

            if (string.IsNullOrWhiteSpace(siteName))
            {
                SetInlineStatus(TxtIisConfigStatus, false, "Chưa nhập IIS Site Name");
                return;
            }

            if (!AppPoolNameValidator.TryValidate(appPoolName, out var validationError))
            {
                SetInlineStatus(TxtAppPoolValidation, false, validationError);
                return;
            }

            SetInlineStatus(TxtAppPoolValidation, null, string.Empty);

            var config = await _client.GetConfigAsync(CancellationToken.None);
            if (config is null)
            {
                SetInlineStatus(TxtIisConfigStatus, false, "Không tải được config");
                return;
            }

            var updated = IisConfigUpdater.ApplyIisConfig(config, siteName, appPoolName);
            var saved = await _client.SaveConfigAsync(updated, CancellationToken.None);
            if (saved is null)
            {
                SetInlineStatus(TxtIisConfigStatus, false, "Lưu thất bại");
                return;
            }

            SetInlineStatus(TxtIisConfigStatus, true, "Đã lưu IIS config");
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtIisConfigStatus, false, ex.Message);
        }
    }

    private void OnAppPoolNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (!AppPoolNameValidator.TryValidate(TxtAppPoolName.Text, out var validationError))
        {
            SetInlineStatus(TxtAppPoolValidation, false, validationError);
            return;
        }

        SetInlineStatus(TxtAppPoolValidation, null, string.Empty);
    }

    private async void OnSaveMaintenance(object? sender, RoutedEventArgs e)
    {
        try
        {
            var request = new MaintenanceModeRequest(
                ChkMaintenanceEnabled.IsChecked == true,
                TxtMaintenanceMessage.Text.Trim());

            var result = await _client.SetFrontendMaintenanceAsync(request, CancellationToken.None);
            if (result is null)
            {
                SetInlineStatus(TxtMaintenanceStatus, null, "--");
                return;
            }

            SetInlineStatus(TxtMaintenanceStatus, result.ExitCode == 0, result.ExitCode == 0 ? "Đã lưu" : result.Stderr);
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtMaintenanceStatus, false, ex.Message);
        }
    }

    private async void OnSaveCompression(object? sender, RoutedEventArgs e)
    {
        try
        {
            var enabled = ChkCompressionEnabled.IsChecked == true;
            var request = new CompressionSettingsDto(enabled, enabled);
            var result = await _client.SetCompressionSettingsAsync(request, CancellationToken.None);
            if (result is null)
            {
                SetInlineStatus(TxtCompressionStatus, null, "--");
                return;
            }

            SetInlineStatus(TxtCompressionStatus, result.ExitCode == 0, result.ExitCode == 0 ? "Đã cập nhật" : result.Stderr);
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtCompressionStatus, false, ex.Message);
        }
    }

    private async void OnClearFrontendCache(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.ClearFrontendCacheAsync(CancellationToken.None);
            if (result is null)
            {
                SetInlineStatus(TxtCompressionStatus, null, "--");
                return;
            }

            SetInlineStatus(TxtCompressionStatus, result.ExitCode == 0, result.ExitCode == 0 ? "Đã xoá cache" : result.Stderr);
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtCompressionStatus, false, ex.Message);
        }
    }

    private async void OnLoadBindings(object? sender, RoutedEventArgs e)
    {
        await LoadBindingsAsync();
    }

    private async void OnApplyBinding(object? sender, RoutedEventArgs e)
    {
        try
        {
            var protocol = TxtBindingProtocol.Text.Trim();
            var ip = TxtBindingIp.Text.Trim();
            var host = TxtBindingHost.Text.Trim();
            if (!int.TryParse(TxtBindingPort.Text.Trim(), out var port))
            {
                TxtBindingsStatus.Text = "Port không hợp lệ";
                return;
            }

            var request = new IisBindingUpdateRequest(
                protocol,
                ip,
                port,
                string.IsNullOrWhiteSpace(host) ? null : host,
                ChkReplaceBinding.IsChecked == true);

            var result = await _client.SetFrontendBindingAsync(request, CancellationToken.None);
            if (result is null)
            {
                SetInlineStatus(TxtBindingsStatus, false, "Update failed");
            }
            else
            {
                SetInlineStatus(TxtBindingsStatus, result.ExitCode == 0, result.ExitCode == 0 ? "Binding updated" : result.Stderr);
            }

            if (result is { ExitCode: 0 })
                await LoadBindingsAsync();
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtBindingsStatus, false, ex.Message);
        }
    }

    private async Task LoadBindingsAsync()
    {
        try
        {
            var bindings = await _client.GetFrontendBindingsAsync(CancellationToken.None) ?? Array.Empty<IisBindingDto>();
            TxtBindings.Text = JsonSerializer.Serialize(bindings, JsonOptions);
            SetInlineStatus(TxtBindingsStatus, true, $"Loaded {bindings.Length}");

            var first = bindings.FirstOrDefault(b => string.Equals(b.Protocol, "http", StringComparison.OrdinalIgnoreCase))
                        ?? bindings.FirstOrDefault();

            if (first is not null)
            {
                TxtBindingProtocol.Text = first.Protocol;
                TxtBindingIp.Text = string.IsNullOrWhiteSpace(first.IpAddress) ? "*" : first.IpAddress;
                TxtBindingPort.Text = first.Port > 0 ? first.Port.ToString() : TxtBindingPort.Text;
                TxtBindingHost.Text = first.Host ?? string.Empty;
                SetFrontendUrlFromBinding(first);
            }
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtBindingsStatus, false, ex.Message);
        }
    }
}
