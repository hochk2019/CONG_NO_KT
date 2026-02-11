using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Ops.Shared.Console;

namespace Ops.Console;

public partial class MainWindow
{
    private async void OnConnect(object? sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedItem is not ConsoleProfile profile)
        {
            TxtConnectStatus.Text = "Chưa chọn profile";
            return;
        }

        ConfigureClient(profile);
        _settings = _settings with
        {
            ActiveProfileId = profile.Id,
            Profiles = MarkProfileUsed(profile.Id)
        };
        SaveSettings();
        SetConnectionState(true, "Đã kết nối");
        TxtActiveProfile.Text = $"{profile.Name} • {profile.BaseUrl}";
        await LoadConfigAsync();
        await RefreshOverviewAsync();
        await LoadFrontendAdvancedAsync();
        await LoadBackendAdvancedAsync();
        await LoadBackupScheduleAsync();
        await LoadPrerequisitesAsync();
    }

    private void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || CmbProfiles.SelectedItem is not ConsoleProfile profile)
            return;

        ApplyProfileToFields(profile);
        TxtActiveProfile.Text = $"{profile.Name} • {profile.BaseUrl}";
        SetConnectionState(true, "Sẵn sàng");
        _settings = _settings with { ActiveProfileId = profile.Id };
        SaveSettings();
    }

    private void OnProfileListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || LstProfiles.SelectedItem is not ConsoleProfile profile)
            return;

        ApplyProfileToFields(profile);
    }

    private void OnProfileAdd(object? sender, RoutedEventArgs e)
    {
        var nextIndex = _settings.Profiles.Count + 1;
        var profile = new ConsoleProfile
        {
            Name = $"Server {nextIndex}",
            BaseUrl = "http://localhost:6090",
            ApiKey = string.Empty
        };

        var profiles = _settings.Profiles.ToList();
        profiles.Add(profile);
        _settings = _settings with { Profiles = profiles };
        SaveSettings();
        LoadProfiles();
        LstProfiles.SelectedItem = profile;
        TxtProfileStatus.Text = "Đã thêm profile";
    }

    private void OnProfileSave(object? sender, RoutedEventArgs e)
    {
        if (LstProfiles.SelectedItem is not ConsoleProfile selected)
        {
            TxtProfileStatus.Text = "Chưa chọn profile";
            return;
        }

        var name = TxtProfileName.Text.Trim();
        var baseUrl = TxtProfileBaseUrl.Text.Trim();
        var apiKey = TxtProfileApiKey.Text.Trim();
        var updated = selected with
        {
            Name = string.IsNullOrWhiteSpace(name) ? selected.Name : name,
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? selected.BaseUrl : baseUrl,
            ApiKey = apiKey
        };

        var profiles = _settings.Profiles.Select(p => p.Id == selected.Id ? updated : p).ToList();
        _settings = _settings with { Profiles = profiles };
        SaveSettings();
        LoadProfiles();
        LstProfiles.SelectedItem = updated;
        TxtProfileStatus.Text = "Đã lưu profile";
    }

    private void OnProfileDelete(object? sender, RoutedEventArgs e)
    {
        if (LstProfiles.SelectedItem is not ConsoleProfile selected)
        {
            TxtProfileStatus.Text = "Chưa chọn profile";
            return;
        }

        if (_settings.Profiles.Count <= 1)
        {
            TxtProfileStatus.Text = "Cần ít nhất 1 profile";
            return;
        }

        var result = MessageBox.Show($"Xoá profile {selected.Name}?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
            return;

        var profiles = _settings.Profiles.Where(p => p.Id != selected.Id).ToList();
        var activeId = _settings.ActiveProfileId == selected.Id
            ? profiles.First().Id
            : _settings.ActiveProfileId;

        _settings = _settings with { Profiles = profiles, ActiveProfileId = activeId };
        SaveSettings();
        LoadProfiles();
        TxtProfileStatus.Text = "Đã xoá profile";
    }

    private void OnProfileSetActive(object? sender, RoutedEventArgs e)
    {
        if (LstProfiles.SelectedItem is not ConsoleProfile selected)
        {
            TxtProfileStatus.Text = "Chưa chọn profile";
            return;
        }

        _settings = _settings with { ActiveProfileId = selected.Id };
        SaveSettings();
        LoadProfiles();
        TxtProfileStatus.Text = "Đã chọn profile";
    }

    private void OnSaveSettings(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtAutoRefreshSeconds.Text.Trim(), out var seconds))
            seconds = _settings.AutoRefreshSeconds;

        var enableAdvanced = ChkAdvancedMode.IsChecked == true;
        if (enableAdvanced && !_settings.AdvancedModeEnabled)
        {
            var confirm = MessageBox.Show(
                "Chế độ nâng cao có thể ảnh hưởng hệ thống. Bạn có chắc muốn bật?",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                ChkAdvancedMode.IsChecked = false;
                return;
            }
        }

        _settings = _settings with { AutoRefreshSeconds = seconds, AdvancedModeEnabled = enableAdvanced };
        SaveSettings();
        UpdateAutoRefresh(seconds);
        ApplyAdvancedVisibility();
        TxtSettingsStatus.Text = "Đã lưu thiết lập";
    }

    private List<ConsoleProfile> MarkProfileUsed(string profileId)
    {
        var now = DateTimeOffset.Now;
        return _settings.Profiles.Select(p => p.Id == profileId ? p with { LastUsedAt = now } : p).ToList();
    }
}
