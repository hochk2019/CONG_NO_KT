# Ops Admin Console Implementation Plan

> [!IMPORTANT]
> **HISTORICAL EXECUTION PLAN**
> Tài liệu này là kế hoạch/thực thi theo thời điểm viết, có thể chứa giả định cũ.
> Nguồn vận hành hiện hành: `DEPLOYMENT_GUIDE_DOCKER.md`, `RUNBOOK.md`, `task.md`.


> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a Windows Ops admin .exe + Agent Windows Service to manage backend/frontend/DB/backup/restore and updates for CongNoGolden.

**Architecture:** A local Agent Windows Service exposes a minimal HTTP API secured by API key. A WPF desktop app connects over LAN to manage services, config, backups, logs, and updates. Shared DTO/config live in a shared library.

**Tech Stack:** .NET 8, WPF, Minimal API, PowerShell process runner, JSON config, xUnit

---

### Task 1: Create ops solution and projects

**Files:**
- Create: `src/ops/CongNoGolden.Ops.sln`
- Create: `src/ops/Ops.Shared/Ops.Shared.csproj`
- Create: `src/ops/Ops.Agent/Ops.Agent.csproj`
- Create: `src/ops/Ops.Console/Ops.Console.csproj`
- Create: `src/ops/Ops.Tests/Ops.Tests.csproj`

**Step 1: Scaffold solution and projects**

Run:
```powershell
dotnet new sln -n CongNoGolden.Ops

dotnet new classlib -n Ops.Shared

dotnet new worker -n Ops.Agent

dotnet new wpf -n Ops.Console

dotnet new xunit -n Ops.Tests

# Add references

dotnet sln CongNoGolden.Ops.sln add .\Ops.Shared\Ops.Shared.csproj

dotnet sln CongNoGolden.Ops.sln add .\Ops.Agent\Ops.Agent.csproj

dotnet sln CongNoGolden.Ops.sln add .\Ops.Console\Ops.Console.csproj

dotnet sln CongNoGolden.Ops.sln add .\Ops.Tests\Ops.Tests.csproj

# Project references

dotnet add .\Ops.Agent\Ops.Agent.csproj reference .\Ops.Shared\Ops.Shared.csproj

dotnet add .\Ops.Console\Ops.Console.csproj reference .\Ops.Shared\Ops.Shared.csproj

dotnet add .\Ops.Tests\Ops.Tests.csproj reference .\Ops.Shared\Ops.Shared.csproj
```

Expected: Projects created under `src/ops` and solution builds.

**Step 2: Run build**

Run:
```powershell
dotnet build .\CongNoGolden.Ops.sln
```
Expected: Build succeeds.

**Step 3: Commit**

Skip unless user asks for commit.

---

### Task 2: Shared config + DTOs + storage

**Files:**
- Create: `src/ops/Ops.Shared/Config/OpsConfig.cs`
- Create: `src/ops/Ops.Shared/Config/ConfigStore.cs`
- Create: `src/ops/Ops.Shared/Models/ServiceStatusDto.cs`
- Test: `src/ops/Ops.Tests/ConfigStoreTests.cs`

**Step 1: Write failing test**

```csharp
using Ops.Shared.Config;

public class ConfigStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsConfig()
    {
        var temp = Path.GetTempFileName();
        var store = new ConfigStore(temp);

        var config = OpsConfig.CreateDefault();
        config.Agent.ApiKey = "test-key";

        store.Save(config);
        var loaded = store.Load();

        Assert.Equal("test-key", loaded.Agent.ApiKey);
    }
}
```

**Step 2: Run test to verify it fails**

Run:
```powershell
dotnet test .\Ops.Tests\Ops.Tests.csproj
```
Expected: FAIL because `ConfigStore` and `OpsConfig` do not exist.

**Step 3: Write minimal implementation**

`src/ops/Ops.Shared/Config/OpsConfig.cs`
```csharp
using System.Text.Json.Serialization;

namespace Ops.Shared.Config;

public sealed record OpsConfig
{
    public AgentConfig Agent { get; init; } = new();
    public BackendConfig Backend { get; init; } = new();
    public FrontendConfig Frontend { get; init; } = new();
    public DatabaseConfig Database { get; init; } = new();
    public PathsConfig Paths { get; init; } = new();

    public static OpsConfig CreateDefault() => new();
}

public sealed record AgentConfig
{
    public string BaseUrl { get; init; } = "http://0.0.0.0:6090";
    public string ApiKey { get; set; } = "";
    public string ConfigPath { get; init; } = "C:\\apps\\congno\\ops\\agent-config.json";
}

public sealed record BackendConfig
{
    public string ServiceName { get; init; } = "CongNoGoldenApi";
    public string BaseUrl { get; init; } = "http://127.0.0.1:8080";
    public string AppPath { get; init; } = "C:\\apps\\congno\\api";
    public string LogPath { get; init; } = "C:\\apps\\congno\\api\\logs\\api.log";
}

public sealed record FrontendConfig
{
    public string IisSiteName { get; init; } = "CongNoGoldenWeb";
    public string AppPath { get; init; } = "C:\\apps\\congno\\web";
}

public sealed record DatabaseConfig
{
    public string ConnectionString { get; init; } = "Host=localhost;Port=5432;Database=congno_golden;Username=postgres;Password=CHANGE_ME";
    public string PgBinPath { get; init; } = "";
}

public sealed record PathsConfig
{
    public string BackupRoot { get; init; } = "C:\\apps\\congno\\backup\\ops";
    public string TempRoot { get; init; } = "C:\\apps\\congno\\ops\\tmp";
}
```

`src/ops/Ops.Shared/Config/ConfigStore.cs`
```csharp
using System.Text.Json;

namespace Ops.Shared.Config;

public sealed class ConfigStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public ConfigStore(string path)
    {
        _path = path;
    }

    public OpsConfig Load()
    {
        if (!File.Exists(_path))
        {
            var created = OpsConfig.CreateDefault();
            Save(created);
            return created;
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<OpsConfig>(json, Options) ?? OpsConfig.CreateDefault();
    }

    public void Save(OpsConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
        var json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(_path, json);
    }
}
```

`src/ops/Ops.Shared/Models/ServiceStatusDto.cs`
```csharp
namespace Ops.Shared.Models;

public sealed record ServiceStatusDto(
    string Name,
    string Status,
    string? Message = null);
```

**Step 4: Run test to verify it passes**

Run:
```powershell
dotnet test .\Ops.Tests\Ops.Tests.csproj
```
Expected: PASS.

**Step 5: Commit**

Skip unless user asks for commit.

---

### Task 3: Agent host + API key middleware + health

**Files:**
- Modify: `src/ops/Ops.Agent/Program.cs`
- Create: `src/ops/Ops.Agent/Security/ApiKeyMiddleware.cs`
- Create: `src/ops/Ops.Agent/Services/AgentState.cs`
- Test: `src/ops/Ops.Tests/ApiKeyMiddlewareTests.cs`

**Step 1: Write failing test**

```csharp
using Microsoft.AspNetCore.Http;
using Ops.Agent.Security;
using Ops.Shared.Config;

public class ApiKeyMiddlewareTests
{
    [Fact]
    public async Task Rejects_WhenMissingApiKey()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new ApiKeyMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, "secret");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }
}
```

**Step 2: Run test to verify it fails**

Run:
```powershell
dotnet test .\Ops.Tests\Ops.Tests.csproj
```
Expected: FAIL because middleware not implemented.

**Step 3: Write minimal implementation**

`src/ops/Ops.Agent/Security/ApiKeyMiddleware.cs`
```csharp
using Microsoft.AspNetCore.Http;

namespace Ops.Agent.Security;

public sealed class ApiKeyMiddleware(RequestDelegate next, string apiKey)
{
    private const string HeaderName = "X-Api-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var value) || value != apiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await next(context);
    }
}
```

`src/ops/Ops.Agent/Services/AgentState.cs`
```csharp
using Ops.Shared.Config;

namespace Ops.Agent.Services;

public sealed class AgentState
{
    public OpsConfig Config { get; private set; }
    public string ConfigPath => Config.Agent.ConfigPath;

    public AgentState(OpsConfig config)
    {
        Config = config;
    }

    public void Update(OpsConfig config) => Config = config;
}
```

`src/ops/Ops.Agent/Program.cs`
```csharp
using Microsoft.Extensions.Hosting.WindowsServices;
using Ops.Agent.Security;
using Ops.Agent.Services;
using Ops.Shared.Config;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

var configPath = builder.Configuration.GetValue<string>("Ops:ConfigPath") ?? "C:\\apps\\congno\\ops\\agent-config.json";
var store = new ConfigStore(configPath);
var opsConfig = store.Load();
if (string.IsNullOrWhiteSpace(opsConfig.Agent.ApiKey))
{
    opsConfig = opsConfig with { Agent = opsConfig.Agent with { ApiKey = Guid.NewGuid().ToString("N") } };
    store.Save(opsConfig);
}

builder.Services.AddSingleton(store);
builder.Services.AddSingleton(new AgentState(opsConfig));

var app = builder.Build();

app.UseMiddleware<ApiKeyMiddleware>(opsConfig.Agent.ApiKey);

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

app.MapGet("/config", (AgentState state) => Results.Ok(state.Config));

app.MapPut("/config", (OpsConfig input, ConfigStore store, AgentState state) =>
{
    store.Save(input);
    state.Update(input);
    return Results.Ok(input);
});

app.Run(opsConfig.Agent.BaseUrl);
```

**Step 4: Run test to verify it passes**

Run:
```powershell
dotnet test .\Ops.Tests\Ops.Tests.csproj
```
Expected: PASS.

**Step 5: Commit**

Skip unless user asks for commit.

---

### Task 4: Service control + IIS control + status

**Files:**
- Create: `src/ops/Ops.Agent/Services/ServiceControl.cs`
- Create: `src/ops/Ops.Agent/Services/IisControl.cs`
- Modify: `src/ops/Ops.Agent/Program.cs`
- Test: `src/ops/Ops.Tests/ServiceControlTests.cs`

**Step 1: Write failing test**

```csharp
using Ops.Agent.Services;

public class ServiceControlTests
{
    [Fact]
    public void ParseServiceStatus_UnknownWhenMissing()
    {
        var status = ServiceControl.NormalizeStatus(null);
        Assert.Equal("unknown", status);
    }
}
```

**Step 2: Run test to verify it fails**

Run:
```powershell
dotnet test .\Ops.Tests\Ops.Tests.csproj
```
Expected: FAIL because ServiceControl not implemented.

**Step 3: Write minimal implementation**

`src/ops/Ops.Agent/Services/ServiceControl.cs`
```csharp
using System.ServiceProcess;
using Ops.Shared.Models;

namespace Ops.Agent.Services;

public sealed class ServiceControl
{
    public static string NormalizeStatus(ServiceControllerStatus? status) => status switch
    {
        ServiceControllerStatus.Running => "running",
        ServiceControllerStatus.Stopped => "stopped",
        ServiceControllerStatus.Paused => "paused",
        ServiceControllerStatus.StartPending => "starting",
        ServiceControllerStatus.StopPending => "stopping",
        _ => "unknown"
    };

    public ServiceStatusDto GetStatus(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return new ServiceStatusDto(serviceName, NormalizeStatus(sc.Status));
        }
        catch
        {
            return new ServiceStatusDto(serviceName, "missing", "Service not found");
        }
    }

    public ServiceStatusDto Start(string serviceName)
    {
        using var sc = new ServiceController(serviceName);
        if (sc.Status == ServiceControllerStatus.Running)
            return new ServiceStatusDto(serviceName, "running");

        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        return new ServiceStatusDto(serviceName, NormalizeStatus(sc.Status));
    }

    public ServiceStatusDto Stop(string serviceName)
    {
        using var sc = new ServiceController(serviceName);
        if (sc.Status == ServiceControllerStatus.Stopped)
            return new ServiceStatusDto(serviceName, "stopped");

        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        return new ServiceStatusDto(serviceName, NormalizeStatus(sc.Status));
    }

    public ServiceStatusDto Restart(string serviceName)
    {
        Stop(serviceName);
        return Start(serviceName);
    }
}
```

`src/ops/Ops.Agent/Services/IisControl.cs`
```csharp
using System.Diagnostics;
using Ops.Shared.Models;

namespace Ops.Agent.Services;

public sealed class IisControl
{
    public async Task<ServiceStatusDto> StartAsync(string siteName, CancellationToken ct)
        => await RunAsync($"Import-Module WebAdministration; Start-Website -Name '{siteName}'", siteName, ct);

    public async Task<ServiceStatusDto> StopAsync(string siteName, CancellationToken ct)
        => await RunAsync($"Import-Module WebAdministration; Stop-Website -Name '{siteName}'", siteName, ct);

    public async Task<ServiceStatusDto> StatusAsync(string siteName, CancellationToken ct)
        => await RunAsync($"Import-Module WebAdministration; (Get-Website -Name '{siteName}').State", siteName, ct, outputOnly: true);

    private static async Task<ServiceStatusDto> RunAsync(string cmd, string name, CancellationToken ct, bool outputOnly = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{cmd}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start PowerShell");
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (!string.IsNullOrWhiteSpace(stderr))
            return new ServiceStatusDto(name, "error", stderr.Trim());

        if (outputOnly)
            return new ServiceStatusDto(name, stdout.Trim().ToLowerInvariant());

        return new ServiceStatusDto(name, "ok", stdout.Trim());
    }
}
```

Modify `Program.cs` to wire services + endpoints:
```csharp
builder.Services.AddSingleton<ServiceControl>();
builder.Services.AddSingleton<IisControl>();

app.MapGet("/status", (AgentState state, ServiceControl svc, IisControl iis, CancellationToken ct) =>
{
    var backend = svc.GetStatus(state.Config.Backend.ServiceName);
    return Results.Ok(new {
        backend,
        frontend = iis.StatusAsync(state.Config.Frontend.IisSiteName, ct).Result
    });
});

app.MapPost("/services/backend/start", (AgentState state, ServiceControl svc) => Results.Ok(svc.Start(state.Config.Backend.ServiceName)));
app.MapPost("/services/backend/stop", (AgentState state, ServiceControl svc) => Results.Ok(svc.Stop(state.Config.Backend.ServiceName)));
app.MapPost("/services/backend/restart", (AgentState state, ServiceControl svc) => Results.Ok(svc.Restart(state.Config.Backend.ServiceName)));

app.MapPost("/services/frontend/start", async (AgentState state, IisControl iis, CancellationToken ct)
    => Results.Ok(await iis.StartAsync(state.Config.Frontend.IisSiteName, ct)));
app.MapPost("/services/frontend/stop", async (AgentState state, IisControl iis, CancellationToken ct)
    => Results.Ok(await iis.StopAsync(state.Config.Frontend.IisSiteName, ct)));
```

**Step 4: Run tests**

Run:
```powershell
dotnet test .\Ops.Tests\Ops.Tests.csproj
```
Expected: PASS.

**Step 5: Commit**

Skip unless user asks for commit.

---

### Task 5: Backup + restore endpoints

**Files:**
- Create: `src/ops/Ops.Agent/Services/BackupRunner.cs`
- Modify: `src/ops/Ops.Agent/Program.cs`
- Test: `src/ops/Ops.Tests/BackupRunnerTests.cs`

**Step 1: Write failing test**

```csharp
using Ops.Agent.Services;

public class BackupRunnerTests
{
    [Fact]
    public void BuildRestoreArgs_IncludesNoOwner()
    {
        var args = BackupRunner.BuildRestoreArgs("file.dump", "congno_golden");
        Assert.Contains("--no-owner", args);
    }
}
```

**Step 2: Run test to verify it fails**

```powershell
dotnet test .\Ops.Tests\Ops.Tests.csproj
```
Expected: FAIL because BackupRunner not implemented.

**Step 3: Write minimal implementation**

`src/ops/Ops.Agent/Services/BackupRunner.cs`
```csharp
using System.Diagnostics;
using Ops.Shared.Config;

namespace Ops.Agent.Services;

public sealed class BackupRunner
{
    public static string BuildDumpArgs(string filePath, string dbName)
        => $"-F c -f \"{filePath}\" {dbName}";

    public static string BuildRestoreArgs(string filePath, string dbName)
        => $"--clean --if-exists --no-owner --no-privileges -d {dbName} \"{filePath}\"";

    public async Task<(int exitCode, string stdout, string stderr)> RunAsync(
        string exePath,
        string args,
        string connectionString,
        CancellationToken ct)
    {
        var pw = ExtractPassword(connectionString);
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (!string.IsNullOrEmpty(pw))
            psi.Environment["PGPASSWORD"] = pw;

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        return (proc.ExitCode, stdout, stderr);
    }

    private static string ExtractPassword(string conn)
    {
        var parts = conn.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
                return part["Password=".Length..];
        }
        return "";
    }
}
```

Modify `Program.cs` endpoints:
```csharp
builder.Services.AddSingleton<BackupRunner>();

app.MapPost("/backup/create", async (AgentState state, BackupRunner runner, CancellationToken ct) =>
{
    Directory.CreateDirectory(state.Config.Paths.BackupRoot);
    var file = Path.Combine(state.Config.Paths.BackupRoot, $"congno_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dump");
    var db = ParseDbName(state.Config.Database.ConnectionString);
    var exe = Path.Combine(state.Config.Database.PgBinPath, "pg_dump.exe");
    var args = BackupRunner.BuildDumpArgs(file, db);

    var result = await runner.RunAsync(exe, args, state.Config.Database.ConnectionString, ct);
    return Results.Ok(new { file, result.exitCode, result.stdout, result.stderr });
});

app.MapPost("/backup/restore", async (AgentState state, BackupRunner runner, string filePath, CancellationToken ct) =>
{
    var db = ParseDbName(state.Config.Database.ConnectionString);
    var exe = Path.Combine(state.Config.Database.PgBinPath, "pg_restore.exe");
    var args = BackupRunner.BuildRestoreArgs(filePath, db);

    var result = await runner.RunAsync(exe, args, state.Config.Database.ConnectionString, ct);
    return Results.Ok(new { result.exitCode, result.stdout, result.stderr });
});

static string ParseDbName(string conn)
{
    var parts = conn.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var part in parts)
    {
        if (part.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
            return part["Database=".Length..];
    }
    return "postgres";
}
```

**Step 4: Run tests**

```powershell
dotnet test .\Ops.Tests\Ops.Tests.csproj
```
Expected: PASS.

**Step 5: Commit**

Skip unless user asks for commit.

---

### Task 6: WPF app skeleton + API client

**Files:**
- Create: `src/ops/Ops.Console/Services/AgentClient.cs`
- Modify: `src/ops/Ops.Console/App.xaml`
- Modify: `src/ops/Ops.Console/MainWindow.xaml`
- Modify: `src/ops/Ops.Console/MainWindow.xaml.cs`

**Step 1: Write minimal API client**

`src/ops/Ops.Console/Services/AgentClient.cs`
```csharp
using System.Net.Http.Json;
using Ops.Shared.Config;
using Ops.Shared.Models;

namespace Ops.Console.Services;

public sealed class AgentClient(HttpClient http)
{
    public void SetApiKey(string key) => http.DefaultRequestHeaders.Remove("X-Api-Key");

    public async Task<object?> GetHealthAsync(CancellationToken ct)
        => await http.GetFromJsonAsync<object>("/health", ct);

    public async Task<OpsConfig?> GetConfigAsync(CancellationToken ct)
        => await http.GetFromJsonAsync<OpsConfig>("/config", ct);

    public async Task<ServiceStatusDto?> StartBackendAsync(CancellationToken ct)
        => await http.PostFromJsonAsync<object>("/services/backend/start", new { }, ct)
            .Result.Content.ReadFromJsonAsync<ServiceStatusDto>(ct);
}
```

**Step 2: Wire MainWindow**

`MainWindow.xaml` (minimal):
```xml
<Window x:Class="Ops.Console.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="CongNo Ops" Height="600" Width="900">
  <Grid Margin="16">
    <TabControl>
      <TabItem Header="Dashboard">
        <StackPanel>
          <Button Name="BtnHealth" Content="Check Health" Margin="0,0,0,8" />
          <TextBlock Name="TxtHealth" />
        </StackPanel>
      </TabItem>
    </TabControl>
  </Grid>
</Window>
```

`MainWindow.xaml.cs`:
```csharp
using System.Net.Http;
using Ops.Console.Services;

namespace Ops.Console;

public partial class MainWindow : Window
{
    private readonly AgentClient _client;

    public MainWindow()
    {
        InitializeComponent();
        var http = new HttpClient { BaseAddress = new Uri("http://localhost:6090") };
        _client = new AgentClient(http);

        BtnHealth.Click += async (_, _) =>
        {
            var health = await _client.GetHealthAsync(CancellationToken.None);
            TxtHealth.Text = health?.ToString() ?? "N/A";
        };
    }
}
```

**Step 3: Build**

```powershell
dotnet build .\Ops.Console\Ops.Console.csproj
```
Expected: PASS.

**Step 4: Commit**

Skip unless user asks for commit.

---

### Task 7: Ops docs

**Files:**
- Create: `docs/OPS_ADMIN_CONSOLE.md`

**Step 1: Add docs**

```markdown
# OPS Admin Console

## Build
- Agent: `dotnet publish -c Release -o C:\apps\congno\ops\agent`
- Console: `dotnet publish -c Release -o C:\apps\congno\ops\console`

## Run Agent (dev)
`dotnet run --project src/ops/Ops.Agent`

## Run Console (dev)
`dotnet run --project src/ops/Ops.Console`
```

**Step 2: Commit**

Skip unless user asks for commit.
```

