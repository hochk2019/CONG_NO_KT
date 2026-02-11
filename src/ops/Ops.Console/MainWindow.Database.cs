using System.Text.Json;
using System.Windows;
using Ops.Shared.Models;

namespace Ops.Console;

public partial class MainWindow
{
    private async Task LoadBackupScheduleAsync()
    {
        try
        {
            var schedule = await _client.GetBackupScheduleAsync(CancellationToken.None);
            if (schedule is null)
                return;

            ChkBackupScheduleEnabled.IsChecked = schedule.Enabled;
            TxtBackupTimeOfDay.Text = schedule.TimeOfDay;
            TxtBackupRetention.Text = schedule.RetentionCount.ToString();
        }
        catch
        {
            // ignore load errors
        }
    }

    private async void OnSaveBackupSchedule(object? sender, RoutedEventArgs e)
    {
        try
        {
            var enabled = ChkBackupScheduleEnabled.IsChecked == true;
            var time = TxtBackupTimeOfDay.Text.Trim();
            var retention = int.TryParse(TxtBackupRetention.Text.Trim(), out var parsed) ? parsed : 0;
            var request = new BackupScheduleDto(enabled, time, retention);
            var result = await _client.UpdateBackupScheduleAsync(request, CancellationToken.None);
            if (result is null)
            {
                SetInlineStatus(TxtBackupScheduleStatus, null, "--");
                return;
            }

            SetInlineStatus(TxtBackupScheduleStatus, true, "Đã lưu lịch backup");
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtBackupScheduleStatus, false, ex.Message);
        }
    }

    private async void OnRefreshBackups(object? sender, RoutedEventArgs e)
    {
        try
        {
            var backups = await _client.GetBackupsAsync(CancellationToken.None) ?? Array.Empty<string>();
            LstBackups.ItemsSource = backups;
        }
        catch (Exception ex)
        {
            TxtBackupResult.Text = ex.Message;
        }
    }

    private async void OnCreateBackup(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.CreateBackupAsync(CancellationToken.None);
            TxtBackupResult.Text = JsonSerializer.Serialize(result, JsonOptions);
            OnRefreshBackups(sender, e);
        }
        catch (Exception ex)
        {
            TxtBackupResult.Text = ex.Message;
        }
    }

    private async void OnRunBackupNow(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.RunBackupNowAsync(CancellationToken.None);
            TxtBackupResult.Text = JsonSerializer.Serialize(result, JsonOptions);
            OnRefreshBackups(sender, e);
        }
        catch (Exception ex)
        {
            TxtBackupResult.Text = ex.Message;
        }
    }

    private async void OnRestore(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = TxtRestorePath.Text.Trim();
            if (string.IsNullOrWhiteSpace(path) && LstBackups.SelectedItem is string selected)
                path = selected;

            if (string.IsNullOrWhiteSpace(path))
            {
                TxtBackupResult.Text = "Thiếu đường dẫn restore";
                return;
            }

            var result = await _client.RestoreBackupAsync(path, CancellationToken.None);
            TxtBackupResult.Text = JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            TxtBackupResult.Text = ex.Message;
        }
    }

    private async void OnCheckDatabase(object? sender, RoutedEventArgs e)
    {
        try
        {
            var diagnostics = await _client.GetDiagnosticsAsync(CancellationToken.None);
            if (diagnostics is null)
            {
                SetInlineStatus(TxtDatabaseInitStatus, null, "--");
                return;
            }

            var message = diagnostics.DatabaseReachable
                ? "Kết nối DB OK"
                : $"Không kết nối được DB: {diagnostics.DatabaseMessage}";
            SetInlineStatus(TxtDatabaseInitStatus, diagnostics.DatabaseReachable, message);
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtDatabaseInitStatus, false, ex.Message);
        }
    }

    private async void OnCreateDatabase(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.CreateDatabaseAsync(CancellationToken.None);
            if (result is null)
            {
                SetInlineStatus(TxtDatabaseInitStatus, null, "--");
                return;
            }

            var message = result.ExitCode == 0 ? result.Stdout : result.Stderr;
            SetInlineStatus(TxtDatabaseInitStatus, result.ExitCode == 0, message);
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtDatabaseInitStatus, false, ex.Message);
        }
    }

    private async void OnRunMigrations(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.RunMigrationsAsync(CancellationToken.None);
            if (result is null)
            {
                SetInlineStatus(TxtDatabaseInitStatus, null, "--");
                return;
            }

            var message = result.ExitCode == 0 ? result.Stdout : result.Stderr;
            SetInlineStatus(TxtDatabaseInitStatus, result.ExitCode == 0, message);
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtDatabaseInitStatus, false, ex.Message);
        }
    }

    private async void OnSqlPreview(object? sender, RoutedEventArgs e)
    {
        await RunSqlAsync(preview: true);
    }

    private async void OnSqlExecute(object? sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Thực thi SQL có thể thay đổi dữ liệu. Bạn có chắc muốn tiếp tục?",
            "Xác nhận",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        await RunSqlAsync(preview: false);
    }

    private async Task RunSqlAsync(bool preview)
    {
        try
        {
            var sql = TxtSqlInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(sql))
            {
                SetInlineStatus(TxtSqlStatus, false, "Chưa nhập SQL");
                return;
            }

            SqlExecuteResponse? response = preview
                ? await _client.PreviewSqlAsync(sql, CancellationToken.None)
                : await _client.ExecuteSqlAsync(sql, CancellationToken.None);

            TxtSqlOutput.Text = response is null ? string.Empty : response.Stdout + Environment.NewLine + response.Stderr;
            if (response is null)
            {
                SetInlineStatus(TxtSqlStatus, null, "--");
                return;
            }

            var status = response.RowsAffected is null
                ? $"Exit {response.ExitCode}"
                : $"Rows: {response.RowsAffected}";
            SetInlineStatus(TxtSqlStatus, response.ExitCode == 0, status);
        }
        catch (Exception ex)
        {
            SetInlineStatus(TxtSqlStatus, false, ex.Message);
        }
    }
}
