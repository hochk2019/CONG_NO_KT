using Ops.Agent.Security;
using Ops.Agent.Services;
using Ops.Shared.Config;
using Ops.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

var configPath = builder.Configuration["Ops:ConfigPath"] ?? "C:\\apps\\congno\\ops\\agent-config.json";
var store = new ConfigStore(configPath);
var opsConfig = store.Load();

if (string.IsNullOrWhiteSpace(opsConfig.Agent.ApiKey))
{
    opsConfig = opsConfig with { Agent = opsConfig.Agent with { ApiKey = Guid.NewGuid().ToString("N") } };
    store.Save(opsConfig);
}

builder.Services.AddSingleton(store);
builder.Services.AddSingleton(new AgentState(opsConfig));
builder.Services.AddSingleton<ServiceControl>();
builder.Services.AddSingleton<ServiceConfigControl>();
builder.Services.AddSingleton<IisControl>();
builder.Services.AddSingleton<DatabaseProbe>();
builder.Services.AddSingleton<BackupRunner>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService<BackupSchedulerHostedService>();
builder.Services.AddSingleton<LogReader>();
builder.Services.AddSingleton<ProcessRunner>();
builder.Services.AddSingleton<UpdateRunner>();
builder.Services.AddSingleton<ServiceInstaller>();
builder.Services.AddSingleton<SystemMetricsProbe>();
builder.Services.AddSingleton<VersionProbe>();
builder.Services.AddSingleton<BackendConfigEditor>();
builder.Services.AddSingleton<FrontendMaintenanceService>();
builder.Services.AddSingleton<DockerRuntimeControl>();
builder.Services.AddSingleton<ISqlCommandRunner, SqlCommandRunner>();
builder.Services.AddSingleton<SqlConsoleService>();
builder.Services.AddSingleton<DatabaseAdminService>();
builder.Services.AddSingleton<PrerequisiteService>();

var app = builder.Build();

app.UseMiddleware<ApiKeyMiddleware>(opsConfig.Agent.ApiKey);

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

app.MapGet("/config", (AgentState state) => Results.Ok(state.Config));

app.MapPut("/config", (OpsConfig input, ConfigStore configStore, AgentState state) =>
{
    configStore.Save(input);
    state.Update(input);
    return Results.Ok(input);
});

app.MapGet("/runtime/info", (AgentState state) =>
{
    var runtime = state.Config.Runtime;
    return Results.Ok(new RuntimeInfoDto(
        runtime.Mode,
        runtime.Docker.ComposeFilePath,
        runtime.Docker.WorkingDirectory,
        runtime.Docker.ProjectName,
        runtime.Docker.BackendService,
        runtime.Docker.FrontendService));
});

app.MapGet("/status", async (AgentState state, ServiceControl services, IisControl iis, DockerRuntimeControl docker, CancellationToken ct) =>
{
    ServiceStatusDto backend;
    ServiceStatusDto frontend;

    if (state.Config.Runtime.IsDockerMode)
    {
        backend = await docker.GetServiceStatusAsync(state.Config, state.Config.Runtime.Docker.BackendService, ct);
        frontend = await docker.GetServiceStatusAsync(state.Config, state.Config.Runtime.Docker.FrontendService, ct);
    }
    else
    {
        backend = services.GetStatus(state.Config.Backend.ServiceName);
        frontend = await iis.StatusAsync(state.Config.Frontend.IisSiteName, ct);
    }

    return Results.Ok(new { backend, frontend });
});

app.MapGet("/metrics/system", async (AgentState state, SystemMetricsProbe probe, CancellationToken ct) =>
{
    var metrics = await probe.GetAsync(state.Config.Backend.AppPath, ct);
    return Results.Ok(metrics);
});

app.MapPost("/services/backend/start", async (AgentState state, ServiceControl services, DockerRuntimeControl docker, CancellationToken ct) =>
{
    if (state.Config.Runtime.IsDockerMode)
    {
        return Results.Ok(await docker.StartServiceAsync(state.Config, state.Config.Runtime.Docker.BackendService, ct));
    }

    return Results.Ok(services.Start(state.Config.Backend.ServiceName));
});

app.MapPost("/services/backend/stop", async (AgentState state, ServiceControl services, DockerRuntimeControl docker, CancellationToken ct) =>
{
    if (state.Config.Runtime.IsDockerMode)
    {
        return Results.Ok(await docker.StopServiceAsync(state.Config, state.Config.Runtime.Docker.BackendService, ct));
    }

    return Results.Ok(services.Stop(state.Config.Backend.ServiceName));
});

app.MapPost("/services/backend/restart", async (AgentState state, ServiceControl services, DockerRuntimeControl docker, CancellationToken ct) =>
{
    if (state.Config.Runtime.IsDockerMode)
    {
        return Results.Ok(await docker.RestartServiceAsync(state.Config, state.Config.Runtime.Docker.BackendService, ct));
    }

    return Results.Ok(services.Restart(state.Config.Backend.ServiceName));
});

app.MapPost("/services/frontend/start", async (AgentState state, IisControl iis, DockerRuntimeControl docker, CancellationToken ct) =>
{
    if (state.Config.Runtime.IsDockerMode)
    {
        return Results.Ok(await docker.StartServiceAsync(state.Config, state.Config.Runtime.Docker.FrontendService, ct));
    }

    return Results.Ok(await iis.StartAsync(state.Config.Frontend.IisSiteName, ct));
});

app.MapPost("/services/frontend/stop", async (AgentState state, IisControl iis, DockerRuntimeControl docker, CancellationToken ct) =>
{
    if (state.Config.Runtime.IsDockerMode)
    {
        return Results.Ok(await docker.StopServiceAsync(state.Config, state.Config.Runtime.Docker.FrontendService, ct));
    }

    return Results.Ok(await iis.StopAsync(state.Config.Frontend.IisSiteName, ct));
});

app.MapGet("/frontend/bindings", async (AgentState state, IisControl iis, CancellationToken ct) =>
{
    var bindings = await iis.GetBindingsAsync(state.Config.Frontend.IisSiteName, ct);
    return Results.Ok(bindings);
});

app.MapPut("/frontend/bindings", async (Ops.Shared.Models.IisBindingUpdateRequest request, AgentState state, IisControl iis, CancellationToken ct) =>
{
    var result = await iis.SetBindingAsync(state.Config.Frontend.IisSiteName, request, ct);
    return Results.Ok(result);
});

app.MapGet("/frontend/app-pool/status", async (AgentState state, IisControl iis, CancellationToken ct) =>
{
    var poolName = string.IsNullOrWhiteSpace(state.Config.Frontend.AppPoolName)
        ? await iis.GetSiteAppPoolAsync(state.Config.Frontend.IisSiteName, ct)
        : state.Config.Frontend.AppPoolName;
    if (string.IsNullOrWhiteSpace(poolName))
        return Results.NotFound();

    var status = await iis.GetAppPoolStatusAsync(poolName, ct);
    return Results.Ok(status);
});

app.MapPost("/frontend/app-pool/start", async (AgentState state, IisControl iis, CancellationToken ct) =>
{
    var poolName = string.IsNullOrWhiteSpace(state.Config.Frontend.AppPoolName)
        ? await iis.GetSiteAppPoolAsync(state.Config.Frontend.IisSiteName, ct)
        : state.Config.Frontend.AppPoolName;
    if (string.IsNullOrWhiteSpace(poolName))
        return Results.NotFound();

    return Results.Ok(await iis.StartAppPoolAsync(poolName, ct));
});

app.MapPost("/frontend/app-pool/stop", async (AgentState state, IisControl iis, CancellationToken ct) =>
{
    var poolName = string.IsNullOrWhiteSpace(state.Config.Frontend.AppPoolName)
        ? await iis.GetSiteAppPoolAsync(state.Config.Frontend.IisSiteName, ct)
        : state.Config.Frontend.AppPoolName;
    if (string.IsNullOrWhiteSpace(poolName))
        return Results.NotFound();

    return Results.Ok(await iis.StopAppPoolAsync(poolName, ct));
});

app.MapPost("/frontend/app-pool/recycle", async (AgentState state, IisControl iis, CancellationToken ct) =>
{
    var poolName = string.IsNullOrWhiteSpace(state.Config.Frontend.AppPoolName)
        ? await iis.GetSiteAppPoolAsync(state.Config.Frontend.IisSiteName, ct)
        : state.Config.Frontend.AppPoolName;
    if (string.IsNullOrWhiteSpace(poolName))
        return Results.NotFound();

    return Results.Ok(await iis.RecycleAppPoolAsync(poolName, ct));
});

app.MapGet("/frontend/compression", async (IisControl iis, CancellationToken ct) =>
    Results.Ok(await iis.GetCompressionSettingsAsync(ct)));

app.MapPut("/frontend/compression", async (Ops.Shared.Models.CompressionSettingsDto request, IisControl iis, CancellationToken ct) =>
    Results.Ok(await iis.SetCompressionSettingsAsync(request, ct)));

app.MapPost("/frontend/cache/clear", () => Results.Ok(IisControl.ClearCompressionCache()));

app.MapGet("/frontend/maintenance", (AgentState state, FrontendMaintenanceService maintenance) =>
    Results.Ok(maintenance.GetStatus(state.Config)));

app.MapPut("/frontend/maintenance", (Ops.Shared.Models.MaintenanceModeRequest request, AgentState state, FrontendMaintenanceService maintenance) =>
    Results.Ok(maintenance.SetMaintenance(state.Config, request)));

app.MapGet("/frontend/version", (AgentState state, VersionProbe versionProbe) =>
{
    var path = Path.Combine(state.Config.Frontend.AppPath, "index.html");
    return Results.Ok(versionProbe.GetComponentVersion("frontend", path));
});

app.MapGet("/backups", (AgentState state) =>
{
    var root = state.Config.Paths.BackupRoot;
    if (!Directory.Exists(root))
    {
        return Results.Ok(Array.Empty<string>());
    }

    var files = Directory.GetFiles(root, "*.dump", SearchOption.TopDirectoryOnly)
        .OrderByDescending(File.GetCreationTimeUtc)
        .ToArray();

    return Results.Ok(files);
});

app.MapPost("/backup/create", async (AgentState state, BackupService backupService, CancellationToken ct) =>
{
    var (file, result) = await backupService.CreateBackupAsync(state.Config, ct);
    return Results.Ok(new { file, result.ExitCode, result.Stdout, result.Stderr });
});

app.MapGet("/backup/schedule", (AgentState state) =>
    Results.Ok(new Ops.Shared.Models.BackupScheduleDto(
        state.Config.BackupSchedule.Enabled,
        state.Config.BackupSchedule.TimeOfDay,
        BackupService.ResolveRetention(state.Config))));

app.MapPut("/backup/schedule", (Ops.Shared.Models.BackupScheduleDto request, ConfigStore configStore, AgentState state) =>
{
    var updated = state.Config with
    {
        BackupSchedule = state.Config.BackupSchedule with
        {
            Enabled = request.Enabled,
            TimeOfDay = request.TimeOfDay,
            RetentionCount = request.RetentionCount
        }
    };
    configStore.Save(updated);
    state.Update(updated);
    return Results.Ok(request);
});

app.MapPost("/backup/run-now", async (AgentState state, BackupService backupService, CancellationToken ct) =>
{
    var (file, result) = await backupService.CreateBackupAsync(state.Config, ct);
    return Results.Ok(new { file, result.ExitCode, result.Stdout, result.Stderr });
});

app.MapPost("/backup/restore", async (Ops.Agent.Models.BackupRestoreRequest request, AgentState state, BackupRunner runner, CancellationToken ct) =>
{
    var connInfo = BackupRunner.ParseConnectionInfo(state.Config.Database.ConnectionString);
    var pgBin = BackupRunner.ResolvePgBinPath(state.Config.Database.PgBinPath);
    var exe = Path.Combine(pgBin, "pg_restore.exe");
    var args = BackupRunner.BuildRestoreArgs(request.FilePath, connInfo);

    var result = await runner.RunAsync(exe, args, state.Config.Database.ConnectionString, ct);
    return Results.Ok(new { result.ExitCode, result.Stdout, result.Stderr });
});

app.MapGet("/logs/tail", (string? path, int? lines, AgentState state, LogReader reader) =>
{
    var resolved = string.IsNullOrWhiteSpace(path) ? state.Config.Backend.LogPath : path;
    if (!reader.IsAllowedPath(resolved, state.Config.Backend.LogPath, state.Config.Paths.LogsRoot))
        return Results.BadRequest(new { error = "Path not allowed" });

    var count = lines is > 0 ? lines.Value : 200;
    var content = reader.ReadTail(resolved, count);
    return Results.Ok(new { path = resolved, lines = count, content });
});

app.MapGet("/diagnostics", async (AgentState state, ServiceControl services, IisControl iis, DatabaseProbe dbProbe, CancellationToken ct) =>
{
    var serviceName = state.Config.Backend.ServiceName;
    var serviceExists = services.Exists(serviceName);
    var exePath = Path.Combine(state.Config.Backend.AppPath, state.Config.Backend.ExeName);
    var exeExists = File.Exists(exePath);
    var iisModule = IisControl.IsModuleAvailable();
    var siteName = state.Config.Frontend.IisSiteName;
    var siteExists = await iis.SiteExistsAsync(siteName, ct);
    var dbResult = await dbProbe.CheckAsync(state.Config.Database.ConnectionString, ct);

    var notes = string.Empty;
    if (!serviceExists)
        notes += "Backend service not installed. ";
    if (!iisModule)
        notes += "IIS WebAdministration module missing. ";
    if (!dbResult.Reachable)
        notes += "Database not reachable. ";

    var result = new Ops.Shared.Models.DiagnosticsResponse(
        serviceName,
        serviceExists,
        exePath,
        exeExists,
        siteName,
        iisModule,
        siteExists,
        dbResult.Host,
        dbResult.Port,
        dbResult.Reachable,
        dbResult.Message,
        notes.Trim());

    return Results.Ok(result);
});

app.MapPost("/update/backend", async (Ops.Agent.Models.UpdateRequest request, AgentState state, UpdateRunner runner, ServiceControl services, CancellationToken ct) =>
{
    var serviceName = state.Config.Backend.ServiceName;
    var warnings = new List<string>();
    if (services.Exists(serviceName))
    {
        var stop = services.Stop(serviceName);
        if (stop.Status == "error")
            warnings.Add($"Stop failed: {stop.Message}");
    }

    var result = await runner.UpdateBackendAsync(state.Config, request.SourcePath, ct);

    if (services.Exists(serviceName))
    {
        var start = services.Start(serviceName);
        if (start.Status == "error")
            warnings.Add($"Start failed: {start.Message}");
    }

    var merged = MergeResult(result, warnings);
    return Results.Ok(merged);
});

app.MapPost("/update/frontend", async (Ops.Agent.Models.UpdateRequest request, AgentState state, UpdateRunner runner, IisControl iis, CancellationToken ct) =>
{
    var warnings = new List<string>();
    if (await iis.SiteExistsAsync(state.Config.Frontend.IisSiteName, ct))
    {
        var stop = await iis.StopAsync(state.Config.Frontend.IisSiteName, ct);
        if (stop.Status == "error")
            warnings.Add($"Stop failed: {stop.Message}");
    }

    var result = await runner.UpdateFrontendAsync(state.Config, request.SourcePath, ct);

    if (await iis.SiteExistsAsync(state.Config.Frontend.IisSiteName, ct))
    {
        var start = await iis.StartAsync(state.Config.Frontend.IisSiteName, ct);
        if (start.Status == "error")
            warnings.Add($"Start failed: {start.Message}");
    }

    var merged = MergeResult(result, warnings);
    return Results.Ok(merged);
});

app.MapPost("/services/backend/install", async (Ops.Agent.Models.InstallServiceRequest request, AgentState state, ServiceInstaller installer, CancellationToken ct) =>
{
    var result = await installer.InstallBackendAsync(state.Config, request.ExePath, ct);
    return Results.Ok(result);
});

app.MapGet("/backend/version", (AgentState state, VersionProbe versionProbe) =>
{
    var path = Path.Combine(state.Config.Backend.AppPath, state.Config.Backend.ExeName);
    return Results.Ok(versionProbe.GetComponentVersion("backend", path));
});

app.MapGet("/backend/service/config", async (AgentState state, ServiceConfigControl serviceConfig, CancellationToken ct) =>
    Results.Ok(await serviceConfig.GetConfigAsync(state.Config.Backend.ServiceName, ct)));

app.MapPut("/backend/service/config", async (Ops.Shared.Models.ServiceConfigUpdateRequest request, ServiceConfigControl serviceConfig, CancellationToken ct) =>
    Results.Ok(await serviceConfig.UpdateConfigAsync(request, ct)));

app.MapGet("/backend/service/recovery", async (AgentState state, ServiceConfigControl serviceConfig, CancellationToken ct) =>
    Results.Ok(await serviceConfig.GetRecoveryAsync(state.Config.Backend.ServiceName, ct)));

app.MapPut("/backend/service/recovery", async (Ops.Shared.Models.ServiceRecoveryUpdateRequest request, ServiceConfigControl serviceConfig, CancellationToken ct) =>
    Results.Ok(await serviceConfig.UpdateRecoveryAsync(request, ct)));

app.MapGet("/backend/log-level", (AgentState state, BackendConfigEditor editor) =>
    Results.Ok(editor.GetLogLevel(state.Config)));

app.MapPut("/backend/log-level", (Ops.Shared.Models.BackendLogLevelUpdateRequest request, AgentState state, BackendConfigEditor editor) =>
    Results.Ok(editor.UpdateLogLevel(state.Config, request.DefaultLevel)));

app.MapGet("/backend/jobs", (AgentState state, BackendConfigEditor editor) =>
    Results.Ok(editor.GetJobSettings(state.Config)));

app.MapPut("/backend/jobs", (Ops.Shared.Models.BackendJobSettingsUpdateRequest request, AgentState state, BackendConfigEditor editor) =>
    Results.Ok(editor.UpdateJobSettings(state.Config, request)));

app.MapPost("/db/sql/preview", async (SqlExecuteRequest request, AgentState state, SqlConsoleService sqlConsole, CancellationToken ct) =>
    Results.Ok(await sqlConsole.ExecuteAsync(state.Config, request.Sql, true, ct)));

app.MapPost("/db/sql/execute", async (SqlExecuteRequest request, AgentState state, SqlConsoleService sqlConsole, CancellationToken ct) =>
    Results.Ok(await sqlConsole.ExecuteAsync(state.Config, request.Sql, false, ct)));

app.MapPost("/db/create", async (AgentState state, DatabaseAdminService databaseAdmin, CancellationToken ct) =>
    Results.Ok(await databaseAdmin.CreateDatabaseAsync(state.Config, ct)));

app.MapPost("/db/migrate", async (AgentState state, DatabaseAdminService databaseAdmin, CancellationToken ct) =>
    Results.Ok(await databaseAdmin.RunMigrationsAsync(state.Config, ct)));

app.MapGet("/prereq", (PrerequisiteService prereq) =>
    Results.Ok(prereq.List()));

app.MapPost("/prereq/install", async (PrereqInstallRequest request, AgentState state, PrerequisiteService prereq, CancellationToken ct) =>
    Results.Ok(await prereq.InstallAsync(state.Config, request.Id, ct)));


static CommandResult MergeResult(CommandResult result, List<string> warnings)
{
    if (warnings.Count == 0)
        return result;

    var combined = string.Join(" ", warnings);
    var stderr = string.IsNullOrWhiteSpace(result.Stderr)
        ? combined
        : $"{result.Stderr} {combined}".Trim();

    var exitCode = result.ExitCode == 0 ? 1 : result.ExitCode;
    return new CommandResult(exitCode, result.Stdout, stderr);
}

app.Run(opsConfig.Agent.BaseUrl);
