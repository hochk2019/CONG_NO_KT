using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Ops.Shared.Config;
using Ops.Shared.Models;

namespace Ops.Console;

public partial class MainWindow
{
    private static readonly Brush StatusOkBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
    private static readonly Brush StatusOkForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"));
    private static readonly Brush StatusWarnBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
    private static readonly Brush StatusWarnForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E"));
    private static readonly Brush StatusErrorBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
    private static readonly Brush StatusErrorForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
    private static readonly Brush StatusNeutralBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
    private static readonly Brush StatusNeutralForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));

    private void OnOpenBackend(object? sender, RoutedEventArgs e)
        => TryOpenUrl(TxtBackendUrl.Text.Trim(), TxtBackendEndpointStatus);

    private void OnOpenFrontend(object? sender, RoutedEventArgs e)
        => TryOpenUrl(TxtFrontendUrl.Text.Trim(), TxtEndpointStatus);

    private static bool TryNormalizeUrl(string input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return false;

        normalized = uri.ToString().TrimEnd('/');
        return true;
    }

    private static void TryOpenUrl(string input, TextBlock statusTarget)
    {
        if (!TryNormalizeUrl(input, out var url))
        {
            statusTarget.Text = "URL không hợp lệ";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            statusTarget.Text = "Đã mở";
        }
        catch (Exception ex)
        {
            statusTarget.Text = ex.Message;
        }
    }

    private void ApplyConfigToEndpointFields(OpsConfig config)
    {
        TxtBackendUrl.Text = config.Backend.BaseUrl;
        TxtFrontendUrl.Text = string.IsNullOrWhiteSpace(config.Frontend.PublicUrl)
            ? TxtFrontendUrl.Text
            : config.Frontend.PublicUrl;
        TxtLogPath.Text = string.IsNullOrWhiteSpace(config.Backend.LogPath) ? TxtLogPath.Text : config.Backend.LogPath;
        TxtDatabaseName.Text = ResolveDatabaseName(config.Database.ConnectionString);
        TxtMigrationPath.Text = ResolveMigrationPath(config);
    }

    private void ApplyStatusBadge(TextBlock textBlock, Border badge, string? status)
    {
        var label = string.IsNullOrWhiteSpace(status) ? "--" : status.Trim();
        var (bg, fg) = MapStatusColors(label);
        badge.Background = bg;
        textBlock.Foreground = fg;
        textBlock.Text = label;
    }

    private void SetInlineStatus(TextBlock target, bool? ok, string message)
    {
        target.Text = message;
        target.Foreground = ok switch
        {
            true => StatusOkForeground,
            false => StatusErrorForeground,
            _ => StatusNeutralForeground
        };
    }

    private (Brush background, Brush foreground) MapStatusColors(string status)
    {
        if (status.Contains("running", StringComparison.OrdinalIgnoreCase)
            || status.Contains("started", StringComparison.OrdinalIgnoreCase)
            || status.Contains("ok", StringComparison.OrdinalIgnoreCase)
            || status.Contains("healthy", StringComparison.OrdinalIgnoreCase))
            return (StatusOkBackground, StatusOkForeground);

        if (status.Contains("stopped", StringComparison.OrdinalIgnoreCase)
            || status.Contains("stop", StringComparison.OrdinalIgnoreCase)
            || status.Contains("error", StringComparison.OrdinalIgnoreCase)
            || status.Contains("fail", StringComparison.OrdinalIgnoreCase))
            return (StatusErrorBackground, StatusErrorForeground);

        if (status.Contains("starting", StringComparison.OrdinalIgnoreCase)
            || status.Contains("stopping", StringComparison.OrdinalIgnoreCase)
            || status.Contains("unknown", StringComparison.OrdinalIgnoreCase)
            || status.Contains("pending", StringComparison.OrdinalIgnoreCase))
            return (StatusWarnBackground, StatusWarnForeground);

        return (StatusNeutralBackground, StatusNeutralForeground);
    }

    private void SetConnectionState(bool connected, string message)
    {
        TxtConnectStatus.Text = message;
        TxtConnectStatus.Foreground = connected ? StatusOkForeground : StatusErrorForeground;
        if (DotConnection is null)
            return;

        var color = connected ? "#22C55E" : "#EF4444";
        DotConnection.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private async Task LoadConfigAsync()
    {
        try
        {
            var config = await _client.GetConfigAsync(CancellationToken.None);
            if (config is not null)
                ApplyConfigToEndpointFields(config);
        }
        catch
        {
            // ignore load errors
        }
    }

    private void SetFrontendUrlFromBinding(IisBindingDto binding)
    {
        if (binding.Port <= 0)
            return;

        var host = !string.IsNullOrWhiteSpace(binding.Host)
            ? binding.Host
            : binding.IpAddress == "*" ? "localhost" : binding.IpAddress;

        var url = $"{binding.Protocol}://{host}:{binding.Port}";
        TxtFrontendUrl.Text = url;
    }

    private static string ResolveDatabaseName(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "--";

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var idx = part.IndexOf('=');
            if (idx <= 0)
                continue;

            var key = part[..idx].Trim();
            if (key.Equals("Database", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
            {
                var value = part[(idx + 1)..].Trim();
                return string.IsNullOrWhiteSpace(value) ? "--" : value;
            }
        }

        return "--";
    }

    private static string ResolveMigrationPath(OpsConfig config)
    {
        var backendPath = System.IO.Path.Combine(config.Backend.AppPath, "scripts", "db", "migrations");
        if (System.IO.Directory.Exists(backendPath))
            return backendPath;

        var repoPath = System.IO.Path.Combine(config.Updates.RepoPath, "scripts", "db", "migrations");
        if (System.IO.Directory.Exists(repoPath))
            return repoPath;

        return $"{backendPath} (chưa tồn tại)";
    }
}
