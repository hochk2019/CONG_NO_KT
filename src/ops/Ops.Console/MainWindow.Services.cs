using System.Windows;
using System.Windows.Controls;
using Ops.Shared.Console;
using Ops.Shared.Models;

namespace Ops.Console;

public partial class MainWindow
{
    private async void OnBackendStart(object? sender, RoutedEventArgs e)
    {
        await UpdateServiceStatusAsync(() => _client.StartBackendAsync(CancellationToken.None), TxtBackendServiceStatus, BadgeBackendServiceStatus);
    }

    private async void OnBackendStop(object? sender, RoutedEventArgs e)
    {
        await UpdateServiceStatusAsync(() => _client.StopBackendAsync(CancellationToken.None), TxtBackendServiceStatus, BadgeBackendServiceStatus);
    }

    private async void OnBackendRestart(object? sender, RoutedEventArgs e)
    {
        await UpdateServiceStatusAsync(() => _client.RestartBackendAsync(CancellationToken.None), TxtBackendServiceStatus, BadgeBackendServiceStatus);
    }

    private async void OnFrontendStart(object? sender, RoutedEventArgs e)
    {
        await UpdateServiceStatusAsync(() => _client.StartFrontendAsync(CancellationToken.None), TxtFrontendServiceStatus, BadgeFrontendServiceStatus);
    }

    private async void OnFrontendStop(object? sender, RoutedEventArgs e)
    {
        await UpdateServiceStatusAsync(() => _client.StopFrontendAsync(CancellationToken.None), TxtFrontendServiceStatus, BadgeFrontendServiceStatus);
    }

    private async void OnAppPoolStart(object? sender, RoutedEventArgs e)
    {
        await UpdateCommandStatusAsync(() => _client.StartAppPoolAsync(CancellationToken.None), TxtAppPoolStatus, BadgeAppPoolStatus);
    }

    private async void OnAppPoolStop(object? sender, RoutedEventArgs e)
    {
        await UpdateCommandStatusAsync(() => _client.StopAppPoolAsync(CancellationToken.None), TxtAppPoolStatus, BadgeAppPoolStatus);
    }

    private async void OnAppPoolRecycle(object? sender, RoutedEventArgs e)
    {
        await UpdateCommandStatusAsync(() => _client.RecycleAppPoolAsync(CancellationToken.None), TxtAppPoolStatus, BadgeAppPoolStatus);
    }

    private async Task UpdateServiceStatusAsync(Func<Task<ServiceStatusDto?>> action, TextBlock target, Border badge)
    {
        try
        {
            var status = await action();
            ApplyStatusBadge(target, badge, status?.Status);
        }
        catch (Exception ex)
        {
            target.Text = ex.Message;
        }
    }

    private async Task UpdateCommandStatusAsync(Func<Task<CommandResponse?>> action, TextBlock target, Border badge)
    {
        try
        {
            var result = await action();
            var label = result is null ? "--" : result.ExitCode == 0 ? "OK" : result.Stderr;
            ApplyStatusBadge(target, badge, label);
        }
        catch (Exception ex)
        {
            target.Text = ex.Message;
        }
    }
}
