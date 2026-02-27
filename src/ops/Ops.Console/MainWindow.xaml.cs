using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Ops.Shared.Config;
using Ops.Shared.Console;

namespace Ops.Console;

public partial class MainWindow : Window
{
    private readonly AgentClient _client = new();
    private readonly ConsoleSettingsStore _settingsStore;
    private ConsoleSettings _settings;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isInitializing;
    private bool _refreshRunning;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public MainWindow()
    {
        InitializeComponent();

        _settingsStore = new ConsoleSettingsStore();
        _settings = _settingsStore.Load();
        _refreshTimer = new DispatcherTimer();

        InitializeStaticOptions();
        HookEvents();
        LoadProfiles();
        ApplyAdvancedVisibility();
        ConfigureAutoRefresh();
        AutoConnect();
    }

    private void InitializeStaticOptions()
    {
        CmbRuntimeMode.ItemsSource = new[] { "windows-service", "docker" };
        CmbLogLevel.ItemsSource = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
        CmbServiceStartMode.ItemsSource = new[] { "auto", "manual", "disabled" };
        CmbRecoveryFirst.ItemsSource = new[] { "restart", "run", "none" };
        CmbRecoverySecond.ItemsSource = new[] { "restart", "run", "none" };
        CmbRecoveryThird.ItemsSource = new[] { "restart", "run", "none" };
    }

    private void HookEvents()
    {
        BtnConnect.Click += OnConnect;
        BtnRefreshOverview.Click += OnRefreshOverview;
        BtnHealth.Click += OnHealth;
        BtnStatus.Click += OnStatus;
        BtnQuickDeployBackend.Click += OnUpdateBackend;
        BtnQuickDeployFrontend.Click += OnUpdateFrontend;
        BtnQuickRestartBackend.Click += OnBackendRestart;
        BtnQuickBackupNow.Click += OnRunBackupNow;

        BtnBackendStart.Click += OnBackendStart;
        BtnBackendStop.Click += OnBackendStop;
        BtnBackendRestart.Click += OnBackendRestart;
        BtnFrontendStart.Click += OnFrontendStart;
        BtnFrontendStop.Click += OnFrontendStop;
        BtnAppPoolStart.Click += OnAppPoolStart;
        BtnAppPoolStop.Click += OnAppPoolStop;
        BtnAppPoolRecycle.Click += OnAppPoolRecycle;

        BtnOpenBackend.Click += OnOpenBackend;
        BtnOpenFrontend.Click += OnOpenFrontend;
        BtnSaveEndpoints.Click += OnSaveEndpoints;
        BtnSaveRuntimeConfig.Click += OnSaveRuntimeConfig;
        CmbRuntimeMode.SelectionChanged += OnRuntimeModeChanged;
        BtnLoadBindings.Click += OnLoadBindings;
        BtnApplyBinding.Click += OnApplyBinding;
        BtnSaveIisConfig.Click += OnSaveIisConfig;
        BtnSaveMaintenance.Click += OnSaveMaintenance;
        BtnSaveCompression.Click += OnSaveCompression;
        BtnClearFrontendCache.Click += OnClearFrontendCache;

        TxtAppPoolName.TextChanged += OnAppPoolNameChanged;

        BtnSaveLogLevel.Click += OnSaveLogLevel;
        BtnSaveJobs.Click += OnSaveJobs;
        BtnSaveServiceConfig.Click += OnSaveServiceConfig;
        BtnSaveRecovery.Click += OnSaveRecovery;

        BtnRefreshBackups.Click += OnRefreshBackups;
        BtnCreateBackup.Click += OnCreateBackup;
        BtnRunBackupNow.Click += OnRunBackupNow;
        BtnRestore.Click += OnRestore;
        BtnCheckDatabase.Click += OnCheckDatabase;
        BtnCreateDatabase.Click += OnCreateDatabase;
        BtnRunMigrations.Click += OnRunMigrations;
        BtnSaveBackupSchedule.Click += OnSaveBackupSchedule;
        BtnSqlPreview.Click += OnSqlPreview;
        BtnSqlExecute.Click += OnSqlExecute;

        BtnLoadLogs.Click += OnLoadLogs;
        BtnUpdateBackend.Click += OnUpdateBackend;
        BtnUpdateFrontend.Click += OnUpdateFrontend;
        BtnLoadPrereqs.Click += OnLoadPrereqs;

        BtnProfileAdd.Click += OnProfileAdd;
        BtnProfileSave.Click += OnProfileSave;
        BtnProfileDelete.Click += OnProfileDelete;
        BtnProfileSetActive.Click += OnProfileSetActive;
        BtnSaveSettings.Click += OnSaveSettings;

        CmbProfiles.SelectionChanged += OnProfileSelectionChanged;
        LstProfiles.SelectionChanged += OnProfileListSelectionChanged;
    }

    private void AutoConnect()
    {
        var profile = GetActiveProfile();
        if (profile is null)
            return;

        ConfigureClient(profile);
        SetConnectionState(true, "Sẵn sàng");
        TxtActiveProfile.Text = $"{profile.Name} • {profile.BaseUrl}";
    }

    private void ConfigureAutoRefresh()
    {
        _refreshTimer.Tick += async (_, _) =>
        {
            if (_refreshRunning)
                return;

            _refreshRunning = true;
            try
            {
                await RefreshOverviewAsync();
            }
            finally
            {
                _refreshRunning = false;
            }
        };

        if (_settings.AutoRefreshSeconds > 0)
        {
            _refreshTimer.Interval = TimeSpan.FromSeconds(_settings.AutoRefreshSeconds);
            _refreshTimer.Start();
        }
    }

    private void UpdateAutoRefresh(int seconds)
    {
        _refreshTimer.Stop();
        if (seconds <= 0)
            return;

        _refreshTimer.Interval = TimeSpan.FromSeconds(seconds);
        _refreshTimer.Start();
    }

    private void LoadProfiles()
    {
        _isInitializing = true;
        var profiles = _settings.Profiles;
        CmbProfiles.ItemsSource = profiles;
        LstProfiles.ItemsSource = profiles;
        var active = GetActiveProfile();
        if (active is not null)
        {
            CmbProfiles.SelectedItem = active;
            LstProfiles.SelectedItem = active;
            ApplyProfileToFields(active);
            TxtActiveProfile.Text = $"{active.Name} • {active.BaseUrl}";
        }

        TxtAutoRefreshSeconds.Text = _settings.AutoRefreshSeconds.ToString();
        ChkAdvancedMode.IsChecked = _settings.AdvancedModeEnabled;
        _isInitializing = false;
    }

    private ConsoleProfile? GetActiveProfile()
        => _settings.Profiles.FirstOrDefault(p => p.Id == _settings.ActiveProfileId)
           ?? _settings.Profiles.FirstOrDefault();

    private void ApplyProfileToFields(ConsoleProfile profile)
    {
        TxtProfileName.Text = profile.Name;
        TxtProfileBaseUrl.Text = profile.BaseUrl;
        TxtProfileApiKey.Text = profile.ApiKey;
    }

    private void ConfigureClient(ConsoleProfile profile)
    {
        _client.Configure(profile.BaseUrl, profile.ApiKey);
    }

    private void SaveSettings()
    {
        _settingsStore.Save(_settings);
    }

    private void ApplyAdvancedVisibility()
    {
        var show = _settings.AdvancedModeEnabled ? Visibility.Visible : Visibility.Collapsed;
        GrpServiceConfig.Visibility = show;
        GrpRecovery.Visibility = show;
        GrpSqlConsole.Visibility = show;
    }

    private void SetStatus(TextBlock target, string? message)
    {
        target.Text = string.IsNullOrWhiteSpace(message) ? "--" : message;
    }
}
