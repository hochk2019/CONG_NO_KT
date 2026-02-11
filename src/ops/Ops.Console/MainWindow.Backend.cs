using System.Globalization;
using System.Windows;
using Ops.Shared.Models;

namespace Ops.Console;

public partial class MainWindow
{
    private async Task LoadBackendAdvancedAsync()
    {
        try
        {
            var logLevel = await _client.GetBackendLogLevelAsync(CancellationToken.None);
            if (logLevel is not null)
                CmbLogLevel.SelectedItem = logLevel.DefaultLevel;

            var jobs = await _client.GetBackendJobsAsync(CancellationToken.None);
            if (jobs is not null)
            {
                ChkJobReminders.IsChecked = jobs.RemindersEnabled;
                ChkJobInvoiceReconcile.IsChecked = jobs.InvoiceReconcileEnabled;
            }

            var service = await _client.GetBackendServiceConfigAsync(CancellationToken.None);
            if (service is not null)
            {
                CmbServiceStartMode.SelectedItem = service.StartMode;
                TxtServiceAccount.Text = service.ServiceAccount;
            }

            var recovery = await _client.GetBackendServiceRecoveryAsync(CancellationToken.None);
            if (recovery is not null)
            {
                CmbRecoveryFirst.SelectedItem = recovery.FirstFailure;
                CmbRecoverySecond.SelectedItem = recovery.SecondFailure;
                CmbRecoveryThird.SelectedItem = recovery.SubsequentFailure;
                var days = recovery.ResetPeriodMinutes / 1440d;
                TxtRecoveryResetDays.Text = days.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // ignore load errors
        }
    }

    private async void OnSaveEndpoints(object? sender, RoutedEventArgs e)
    {
        try
        {
            var backendUrl = TxtBackendUrl.Text.Trim();
            var frontendUrl = TxtFrontendUrl.Text.Trim();

            if (!TryNormalizeUrl(backendUrl, out var normalizedBackend))
            {
            SetInlineStatus(TxtBackendEndpointStatus, false, "Backend URL không hợp lệ");
            return;
        }

            if (!TryNormalizeUrl(frontendUrl, out var normalizedFrontend))
            {
            SetInlineStatus(TxtBackendEndpointStatus, false, "Frontend URL không hợp lệ");
            return;
        }

            var config = await _client.GetConfigAsync(CancellationToken.None);
            if (config is null)
            {
            SetInlineStatus(TxtBackendEndpointStatus, false, "Không tải được config");
            return;
        }

            var updated = config with
            {
                Backend = config.Backend with { BaseUrl = normalizedBackend },
                Frontend = config.Frontend with { PublicUrl = normalizedFrontend }
            };

            var saved = await _client.SaveConfigAsync(updated, CancellationToken.None);
            if (saved is null)
            {
                SetInlineStatus(TxtBackendEndpointStatus, false, "Save failed");
            }
            else
            {
                SetInlineStatus(TxtBackendEndpointStatus, true, "Đã lưu");
            }
            if (saved is not null)
                ApplyConfigToEndpointFields(saved);
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtBackendEndpointStatus, false, ex.Message);
        }
    }

    private async void OnSaveLogLevel(object? sender, RoutedEventArgs e)
    {
        try
        {
            var level = CmbLogLevel.SelectedItem?.ToString() ?? "Information";
            var result = await _client.UpdateBackendLogLevelAsync(new BackendLogLevelUpdateRequest(level), CancellationToken.None);
            if (result is null)
            {
                SetInlineStatus(TxtLogLevelStatus, null, "--");
                return;
            }

            SetInlineStatus(TxtLogLevelStatus, true, $"Đã đặt {result.DefaultLevel}");
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtLogLevelStatus, false, ex.Message);
        }
    }

    private async void OnSaveJobs(object? sender, RoutedEventArgs e)
    {
        try
        {
            var request = new BackendJobSettingsUpdateRequest(
                ChkJobReminders.IsChecked == true,
                ChkJobInvoiceReconcile.IsChecked == true);
            var result = await _client.UpdateBackendJobsAsync(request, CancellationToken.None);
            if (result is null)
            {
                SetInlineStatus(TxtJobStatus, null, "--");
                return;
            }

            SetInlineStatus(TxtJobStatus, true, "Đã lưu cấu hình");
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtJobStatus, false, ex.Message);
        }
    }

    private async void OnSaveServiceConfig(object? sender, RoutedEventArgs e)
    {
        try
        {
            var config = await _client.GetConfigAsync(CancellationToken.None);
            if (config is null)
            {
                SetInlineStatus(TxtServiceConfigStatus, false, "Không tải được config");
                return;
            }

            var request = new ServiceConfigUpdateRequest(
                config.Backend.ServiceName,
                CmbServiceStartMode.SelectedItem?.ToString() ?? "auto",
                string.IsNullOrWhiteSpace(TxtServiceAccount.Text) ? null : TxtServiceAccount.Text.Trim(),
                string.IsNullOrWhiteSpace(TxtServicePassword.Password) ? null : TxtServicePassword.Password);

            var result = await _client.UpdateBackendServiceConfigAsync(request, CancellationToken.None);
            if (result is null)
            {
                SetInlineStatus(TxtServiceConfigStatus, null, "--");
                return;
            }

            SetInlineStatus(TxtServiceConfigStatus, result.ExitCode == 0, result.ExitCode == 0 ? "Đã cập nhật" : result.Stderr);
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtServiceConfigStatus, false, ex.Message);
        }
    }

    private async void OnSaveRecovery(object? sender, RoutedEventArgs e)
    {
        try
        {
            var config = await _client.GetConfigAsync(CancellationToken.None);
            if (config is null)
            {
                SetInlineStatus(TxtRecoveryStatus, false, "Không tải được config");
                return;
            }

            var days = ParseDoubleOrDefault(TxtRecoveryResetDays.Text.Trim());
            var minutes = (int)Math.Max(days * 1440, 0);
            var request = new ServiceRecoveryUpdateRequest(
                config.Backend.ServiceName,
                CmbRecoveryFirst.SelectedItem?.ToString() ?? "restart",
                CmbRecoverySecond.SelectedItem?.ToString() ?? "restart",
                CmbRecoveryThird.SelectedItem?.ToString() ?? "none",
                minutes);

            var result = await _client.UpdateBackendServiceRecoveryAsync(request, CancellationToken.None);
            if (result is null)
            {
                SetInlineStatus(TxtRecoveryStatus, null, "--");
                return;
            }

            SetInlineStatus(TxtRecoveryStatus, result.ExitCode == 0, result.ExitCode == 0 ? "Đã cập nhật" : result.Stderr);
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtRecoveryStatus, false, ex.Message);
        }
    }

    private static double ParseDoubleOrDefault(string input)
    {
        return double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }
}
