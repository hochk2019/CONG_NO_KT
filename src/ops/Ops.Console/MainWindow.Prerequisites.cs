using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Ops.Shared.Models;

namespace Ops.Console;

public partial class MainWindow
{
    private async Task LoadPrerequisitesAsync()
    {
        TxtPrereqStatus.Text = "Đang kiểm tra...";
        try
        {
            var items = await _client.GetPrerequisitesAsync(CancellationToken.None);
            if (items is null)
            {
                TxtPrereqStatus.Text = "Không lấy được danh sách";
                return;
            }

            var view = items.Select(ToViewItem).ToList();
            LstPrereqs.ItemsSource = view;
            TxtPrereqStatus.Text = $"Đã tải {view.Count} mục";
        }
        catch (Exception ex)
        {
            TxtPrereqStatus.Text = ex.Message;
        }
    }

    private async void OnLoadPrereqs(object? sender, RoutedEventArgs e)
        => await LoadPrerequisitesAsync();

    private async void OnInstallPrereq(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id)
            return;

        var confirm = MessageBox.Show($"Cài đặt gói: {id}?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        TxtPrereqStatus.Text = "Đang cài đặt...";
        try
        {
            var result = await _client.InstallPrerequisiteAsync(id, CancellationToken.None);
            TxtPrereqStatus.Text = result is null
                ? "Không nhận được phản hồi"
                : $"{result.ExitCode}: {result.Stdout} {result.Stderr}".Trim();
        }
        catch (Exception ex)
        {
            TxtPrereqStatus.Text = ex.Message;
        }

        await LoadPrerequisitesAsync();
    }

    private void OnOpenPrereqLink(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string url || string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            TxtPrereqStatus.Text = ex.Message;
        }
    }

    private static PrereqViewItem ToViewItem(PrereqItemDto dto)
    {
        var status = dto.IsInstalled ? "Đã cài" : "Chưa cài";
        var version = dto.IsInstalled && !string.IsNullOrWhiteSpace(dto.Version)
            ? $"Phiên bản: {dto.Version}"
            : string.Empty;

        return new PrereqViewItem(
            dto.Id,
            dto.Name,
            dto.Description,
            dto.DownloadUrl,
            status,
            version,
            !dto.IsInstalled,
            dto.Notes ?? string.Empty);
    }

    private sealed record PrereqViewItem(
        string Id,
        string Name,
        string Description,
        string DownloadUrl,
        string StatusText,
        string VersionText,
        bool CanInstall,
        string Notes);
}
