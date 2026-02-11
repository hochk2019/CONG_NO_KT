using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ops.Launcher;

public partial class MainWindow : Window
{
    private const string ServiceName = "CongNoOpsAgent";
    private readonly string _opsRoot = @"C:\apps\congno\ops";
    private readonly string _backendRoot = @"C:\apps\congno\api";
    private readonly string _frontendRoot = @"C:\apps\congno\web";
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();
        HookEvents();
        InitializePaths();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();

        RefreshStatus();
    }

    private string AgentExe => Path.Combine(_opsRoot, "agent", "Ops.Agent.exe");
    private string ConsoleExe => Path.Combine(_opsRoot, "console", "Ops.Console.exe");
    private string AgentConfig => Path.Combine(_opsRoot, "agent-config.json");
    private string LogsDir => Path.Combine(_opsRoot, "logs");

    private void HookEvents()
    {
        BtnRefresh.Click += (_, _) => RefreshStatus();
        BtnStartAgent.Click += async (_, _) => await RunServiceActionAsync("Start", sc => sc.Start());
        BtnStopAgent.Click += async (_, _) => await RunServiceActionAsync("Stop", sc => sc.Stop());
        BtnRestartAgent.Click += async (_, _) => await RunServiceActionAsync("Restart", RestartService);

        BtnOpenAgentLogs.Click += (_, _) => OpenFolder(LogsDir);
        BtnOpenAgentConfig.Click += (_, _) => OpenFile(AgentConfig);
        BtnOpenConsole.Click += (_, _) => OpenFile(ConsoleExe);
        BtnOpenConsoleFolder.Click += (_, _) => OpenFolder(Path.GetDirectoryName(ConsoleExe));
        BtnOpenOpsFolder.Click += (_, _) => OpenFolder(_opsRoot);
        BtnOpenLogsFolder.Click += (_, _) => OpenFolder(LogsDir);
        BtnOpenDeployFolder.Click += (_, _) => OpenFolder(@"C:\apps\congno");
    }

    private void InitializePaths()
    {
        TxtOpsRoot.Text = _opsRoot;
        TxtBackendRoot.Text = _backendRoot;
        TxtFrontendRoot.Text = _frontendRoot;
    }

    private void RefreshStatus()
    {
        UpdateAgentStatus();
        UpdateConsoleStatus();
        TxtStatus.Text = $"Cập nhật lúc {DateTime.Now:HH:mm:ss}";
    }

    private void UpdateAgentStatus()
    {
        try
        {
            using var controller = new ServiceController(ServiceName);
            var status = controller.Status;
            var label = status switch
            {
                ServiceControllerStatus.Running => "Đang chạy",
                ServiceControllerStatus.Stopped => "Đã dừng",
                ServiceControllerStatus.StartPending => "Đang khởi động",
                ServiceControllerStatus.StopPending => "Đang dừng",
                ServiceControllerStatus.Paused => "Tạm dừng",
                _ => status.ToString()
            };

            TxtAgentStatus.Text = label;
            TxtHeaderStatus.Text = $"Agent: {label}";
            SetBadge(BadgeAgentStatus, status == ServiceControllerStatus.Running ? "success" : "warning");
            DotHeaderStatus.Fill = GetBrush(status == ServiceControllerStatus.Running ? "BrushSuccess" : "BrushWarning");
        }
        catch (InvalidOperationException)
        {
            TxtAgentStatus.Text = "Chưa cài service";
            TxtHeaderStatus.Text = "Agent: chưa cài";
            SetBadge(BadgeAgentStatus, "danger");
            DotHeaderStatus.Fill = GetBrush("BrushDanger");
        }
        catch (Exception ex)
        {
            TxtAgentStatus.Text = "Không có quyền";
            TxtHeaderStatus.Text = "Agent: lỗi quyền";
            SetBadge(BadgeAgentStatus, "danger");
            DotHeaderStatus.Fill = GetBrush("BrushDanger");
            LauncherLogger.Error("Không đọc được trạng thái service", ex);
        }

        TxtAgentVersion.Text = GetFileVersion(AgentExe) ?? "Version: --";
        TxtAgentPath.Text = AgentExe;
    }

    private void UpdateConsoleStatus()
    {
        var exists = File.Exists(ConsoleExe);
        TxtConsoleStatus.Text = exists ? "Sẵn sàng" : "Thiếu file";
        SetBadge(BadgeConsoleStatus, exists ? "success" : "danger");
        TxtConsoleVersion.Text = exists ? (GetFileVersion(ConsoleExe) ?? "Version: --") : "Version: --";
        TxtConsolePath.Text = ConsoleExe;
    }

    private async Task RunServiceActionAsync(string actionName, Action<ServiceController> action)
    {
        TxtStatus.Text = $"Đang {actionName.ToLower()} service...";
        LauncherLogger.Info($"{actionName} service requested");

        try
        {
            await Task.Run(() =>
            {
                using var controller = new ServiceController(ServiceName);
                action(controller);
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            });
        }
        catch (InvalidOperationException ex)
        {
            TxtStatus.Text = "Không tìm thấy service CongNoOpsAgent";
            LauncherLogger.Error("Service not found", ex);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Thao tác thất bại: {ex.Message}";
            LauncherLogger.Error("Service action failed", ex);
        }
        finally
        {
            RefreshStatus();
        }
    }

    private static void RestartService(ServiceController controller)
    {
        if (controller.Status != ServiceControllerStatus.Stopped)
        {
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
        }

        controller.Start();
    }

    private void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Không mở được thư mục: {ex.Message}";
            LauncherLogger.Error("Open folder failed", ex);
        }
    }

    private void OpenFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            TxtStatus.Text = "Không tìm thấy file để mở";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Không mở được file: {ex.Message}";
            LauncherLogger.Error("Open file failed", ex);
        }
    }

    private static string? GetFileVersion(string path)
    {
        if (!File.Exists(path))
            return null;

        var info = FileVersionInfo.GetVersionInfo(path);
        return string.IsNullOrWhiteSpace(info.FileVersion) ? null : $"Version: {info.FileVersion}";
    }

    private Brush GetBrush(string key)
        => (Brush)FindResource(key);

    private void SetBadge(Border badge, string state)
    {
        badge.Background = state switch
        {
            "success" => GetBrush("BrushSuccess"),
            "warning" => GetBrush("BrushWarning"),
            "danger" => GetBrush("BrushDanger"),
            _ => GetBrush("BrushBorder")
        };

        if (badge.Child is TextBlock text)
            text.Foreground = Brushes.White;
    }
}
