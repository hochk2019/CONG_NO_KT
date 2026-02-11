using System.IO;
using System.Text.Json;
using System.Windows;

namespace Ops.Console;

public partial class MainWindow
{
    private async void OnUpdateBackend(object? sender, RoutedEventArgs e)
    {
        await RunUpdateAsync(isBackend: true);
    }

    private async void OnUpdateFrontend(object? sender, RoutedEventArgs e)
    {
        await RunUpdateAsync(isBackend: false);
    }

    private async Task RunUpdateAsync(bool isBackend)
    {
        try
        {
            TxtUpdateResult.Text = "Đang triển khai...";
            if (ChkDeployBackup.IsChecked == true)
            {
                var backup = await _client.CreateBackupAsync(CancellationToken.None);
                TxtUpdateResult.Text = $"Backup: {JsonSerializer.Serialize(backup, JsonOptions)}";
            }

            var source = TxtUpdateSource.Text.Trim();
            if (string.IsNullOrWhiteSpace(source))
            {
                var auto = ResolvePayloadSource(isBackend);
                if (!string.IsNullOrWhiteSpace(auto))
                {
                    source = auto;
                    TxtUpdateSource.Text = auto;
                }
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                TxtUpdateResult.Text = "Không tìm thấy source deploy. Hãy chọn thư mục chứa backend/frontend hoặc đặt đúng cấu trúc payload.";
                return;
            }
            var result = isBackend
                ? await _client.UpdateBackendAsync(source, CancellationToken.None)
                : await _client.UpdateFrontendAsync(source, CancellationToken.None);

            TxtUpdateResult.Text = JsonSerializer.Serialize(result, JsonOptions);

            if (ChkDeployVerifyHealth.IsChecked == true)
            {
                var health = await _client.GetHealthAsync(CancellationToken.None);
                TxtUpdateResult.Text += Environment.NewLine + JsonSerializer.Serialize(health, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            TxtUpdateResult.Text = ex.Message;
        }
    }

    private static string ResolvePayloadSource(bool isBackend)
    {
        var target = isBackend ? "backend" : "frontend";
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "payload", target)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "payload", target)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "payload", target)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "payload", target))
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return string.Empty;
    }
}
