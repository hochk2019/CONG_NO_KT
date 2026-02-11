using System.Net.Http;
using Ops.Shared.Config;
using Ops.Shared.Models;

namespace Ops.Agent.Services;

public sealed class PrerequisiteService(ProcessRunner runner)
{
    private static readonly HttpClient Http = new();

    public IReadOnlyList<PrereqItemDto> List()
        => PrerequisiteCatalog.Definitions.Select(def =>
        {
            var installed = def.IsInstalled();
            return new PrereqItemDto(
                def.Id,
                def.Name,
                def.Description,
                installed,
                installed ? def.GetVersion() : null,
                def.DownloadUrl,
                def.RequiresRestart,
                def.Notes);
        }).ToList();

    public async Task<CommandResult> InstallAsync(OpsConfig config, string id, CancellationToken ct)
    {
        var definition = PrerequisiteCatalog.Definitions.FirstOrDefault(x => x.Id == id);
        if (definition is null)
            return new CommandResult(1, string.Empty, "Không tìm thấy package yêu cầu");

        if (definition.IsInstalled())
            return new CommandResult(0, "Đã cài đặt trước đó", string.Empty);

        var tempRoot = string.IsNullOrWhiteSpace(config.Paths.TempRoot)
            ? Path.GetTempPath()
            : config.Paths.TempRoot;
        Directory.CreateDirectory(tempRoot);

        var fileName = GetFileName(definition.DownloadUrl, definition.InstallerType);
        var targetPath = Path.Combine(tempRoot, fileName);

        var download = await DownloadFileAsync(definition.DownloadUrl, targetPath, ct);
        if (!download.Success)
            return new CommandResult(1, string.Empty, download.Error ?? "Không tải được file cài đặt");

        return await RunInstallerAsync(definition, targetPath, tempRoot, ct);
    }

    private async Task<CommandResult> RunInstallerAsync(PrerequisiteDefinition definition, string installerPath, string workDir, CancellationToken ct)
    {
        if (definition.InstallerType == InstallerType.Msi)
        {
            var args = $"/i \"{installerPath}\" {definition.InstallArgs}".Trim();
            return await runner.RunAsync("msiexec", args, workDir, ct);
        }

        return await runner.RunAsync(installerPath, definition.InstallArgs, workDir, ct);
    }

    private static async Task<(bool Success, string? Error)> DownloadFileAsync(string url, string targetPath, CancellationToken ct)
    {
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return (false, $"HTTP {(int)response.StatusCode} khi tải file");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(targetPath);
            await stream.CopyToAsync(output, ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string GetFileName(string url, InstallerType type)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var name = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return type == InstallerType.Msi
            ? $"prereq_{Guid.NewGuid():N}.msi"
            : $"prereq_{Guid.NewGuid():N}.exe";
    }
}

public enum InstallerType
{
    Exe,
    Msi
}

public sealed record PrerequisiteDefinition(
    string Id,
    string Name,
    string Description,
    string DownloadUrl,
    InstallerType InstallerType,
    string InstallArgs,
    Func<bool> IsInstalled,
    Func<string?> GetVersion,
    bool RequiresRestart,
    string? Notes);

public static class PrerequisiteCatalog
{
    public static IReadOnlyList<PrerequisiteDefinition> Definitions { get; } = new[]
    {
        new PrerequisiteDefinition(
            "dotnet-hosting-8",
            ".NET 8 Hosting Bundle",
            "Bắt buộc cho Backend chạy trên IIS/Windows Service.",
            "https://download.visualstudio.microsoft.com/download/pr/32b7d7b8-2d36-4a65-8b77-4a0b4f7ad47f/5a4c1db8b7bce9a9b06c8e3f9c5a3b1c/dotnet-hosting-8.0.13-win.exe",
            InstallerType.Exe,
            "/install /quiet /norestart",
            () => HasDotnetRuntime("Microsoft.AspNetCore.App", 8, out _),
            () => GetDotnetRuntimeVersion("Microsoft.AspNetCore.App", 8),
            true,
            "Cần restart IIS sau khi cài."),
        new PrerequisiteDefinition(
            "dotnet-desktop-8",
            ".NET 8 Desktop Runtime",
            "Cần để chạy Ops Console (WPF).",
            "https://download.visualstudio.microsoft.com/download/pr/ff3d3a9f-1f5b-4c2d-9d66-c8b4e5c6c734/6a30b6909bca2a1d7fe9d8d4a8c9fb31/windowsdesktop-runtime-8.0.13-win-x64.exe",
            InstallerType.Exe,
            "/install /quiet /norestart",
            () => HasDotnetRuntime("Microsoft.WindowsDesktop.App", 8, out _),
            () => GetDotnetRuntimeVersion("Microsoft.WindowsDesktop.App", 8),
            false,
            null),
        new PrerequisiteDefinition(
            "nodejs-lts",
            "Node.js LTS",
            "Chỉ cần khi deploy frontend ở chế độ Git (npm build).",
            "https://nodejs.org/dist/v20.11.1/node-v20.11.1-x64.msi",
            InstallerType.Msi,
            "/quiet /norestart",
            () => HasCommandOnPath("node.exe"),
            () => GetCommandVersion("node", "--version"),
            false,
            null),
        new PrerequisiteDefinition(
            "iis-url-rewrite",
            "IIS URL Rewrite",
            "Cần để frontend SPA hoạt động đúng (rewrite về index.html).",
            "https://download.microsoft.com/download/1/3/4/1349C3F4-3B4B-4C36-8C6E-C5E7C5F3FCCA/rewrite_2.1_x64.msi",
            InstallerType.Msi,
            "/quiet /norestart",
            () => File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "rewrite.dll"))
                  || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", "inetsrv", "rewrite.dll")),
            () => null,
            true,
            "Cần restart IIS sau khi cài.")
    };

    private static bool HasDotnetRuntime(string runtimeName, int minMajor, out string? version)
    {
        version = GetDotnetRuntimeVersion(runtimeName, minMajor);
        return !string.IsNullOrWhiteSpace(version);
    }

    private static string? GetDotnetRuntimeVersion(string runtimeName, int minMajor)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared", runtimeName);
        if (!Directory.Exists(root))
            return null;

        var versions = Directory.GetDirectories(root)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => Version.TryParse(x, out var v) ? v : null)
            .Where(v => v is not null && v.Major >= minMajor)
            .Select(v => v!)
            .OrderByDescending(v => v)
            .ToList();

        return versions.Count == 0 ? null : versions[0].ToString();
    }

    private static bool HasCommandOnPath(string exeName)
        => FindOnPath(exeName) is not null;

    private static string? FindOnPath(string exeName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            var candidate = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? GetCommandVersion(string command, string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null)
                return null;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }
}
