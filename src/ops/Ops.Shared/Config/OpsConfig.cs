using System;
using System.Collections.Generic;

namespace Ops.Shared.Config;

public sealed record OpsConfig
{
    public AgentConfig Agent { get; init; } = new();
    public BackendConfig Backend { get; init; } = new();
    public FrontendConfig Frontend { get; init; } = new();
    public RuntimeConfig Runtime { get; init; } = new();
    public DatabaseConfig Database { get; init; } = new();
    public BackupScheduleConfig BackupSchedule { get; init; } = new();
    public PathsConfig Paths { get; init; } = new();
    public SecurityConfig Security { get; init; } = new();
    public UpdateConfig Updates { get; init; } = new();

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
    public string ExeName { get; init; } = "CongNoGolden.Api.exe";
    public string AppSettingsPath { get; init; } = "";
}

public sealed record FrontendConfig
{
    public string IisSiteName { get; init; } = "CongNoGoldenWeb";
    public string AppPoolName { get; init; } = "CongNoGoldenWeb";
    public string AppPath { get; init; } = "C:\\apps\\congno\\web";
    public string PublicUrl { get; init; } = "http://localhost:8081";
    public string LogPath { get; init; } = "C:\\inetpub\\logs\\LogFiles";
}

public sealed record RuntimeConfig
{
    // windows-service | docker
    public string Mode { get; init; } = "windows-service";
    public DockerRuntimeConfig Docker { get; init; } = new();

    public bool IsDockerMode =>
        string.Equals(Mode, "docker", StringComparison.OrdinalIgnoreCase);
}

public sealed record DockerRuntimeConfig
{
    public string ComposeFilePath { get; init; } = "C:\\apps\\congno\\docker-compose.yml";
    public string WorkingDirectory { get; init; } = "C:\\apps\\congno";
    public string ProjectName { get; init; } = "congno";
    public string BackendService { get; init; } = "api";
    public string FrontendService { get; init; } = "web";
}

public sealed record DatabaseConfig
{
    public string ConnectionString { get; init; } = "Host=localhost;Port=5432;Database=congno_golden;Username=postgres;Password=CHANGE_ME";
    public string PgBinPath { get; init; } = "";
    public int RetentionCount { get; init; } = 7;
}

public sealed record BackupScheduleConfig
{
    public bool Enabled { get; init; } = true;
    public string TimeOfDay { get; init; } = "23:00";
    public int RetentionCount { get; init; } = 7;
}

public sealed record PathsConfig
{
    public string BackupRoot { get; init; } = "C:\\apps\\congno\\backup\\ops";
    public string TempRoot { get; init; } = "C:\\apps\\congno\\ops\\tmp";
    public string LogsRoot { get; init; } = "C:\\apps\\congno\\ops\\logs";
}

public sealed record SecurityConfig
{
    public string AdminUser { get; init; } = "admin";
    public string AdminPassword { get; init; } = "CHANGE_ME";
    public List<string> AllowedWindowsUsers { get; init; } = new();
}

public sealed record UpdateConfig
{
    public string Mode { get; init; } = "copy"; // copy | git
    public string RepoPath { get; init; } = "C:\\apps\\congno\\repo";
    public string BackendPublishPath { get; init; } = "C:\\apps\\congno\\api";
    public string FrontendPublishPath { get; init; } = "C:\\apps\\congno\\web";
    public string NssmPath { get; init; } = "C:\\apps\\congno\\tools\\nssm.exe";
}
