using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using Ops.Shared.Models;

namespace Ops.Agent.Services;

[SupportedOSPlatform("windows")]
public sealed class IisControl
{
    private const string WebAdminPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\Modules\WebAdministration";

    public static bool IsModuleAvailable() => Directory.Exists(WebAdminPath);

    public Task<ServiceStatusDto> StartAsync(string siteName, CancellationToken ct)
        => RunAsync($"Import-Module WebAdministration; Start-Website -Name '{siteName}'", siteName, ct);

    public Task<ServiceStatusDto> StopAsync(string siteName, CancellationToken ct)
        => RunAsync($"Import-Module WebAdministration; Stop-Website -Name '{siteName}'", siteName, ct);

    public Task<ServiceStatusDto> StatusAsync(string siteName, CancellationToken ct)
        => RunAsync($"Import-Module WebAdministration; (Get-Website -Name '{siteName}').State", siteName, ct, outputOnly: true);

    public async Task<bool> SiteExistsAsync(string siteName, CancellationToken ct)
    {
        var appcmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "appcmd.exe");
        if (!File.Exists(appcmd))
            return false;

        var psi = new ProcessStartInfo
        {
            FileName = appcmd,
            Arguments = $"list site \"{siteName}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc is null)
            return false;

        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0 || !string.IsNullOrWhiteSpace(stderr))
            return false;

        return !string.IsNullOrWhiteSpace(stdout);
    }

    public async Task<IisBindingDto[]> GetBindingsAsync(string siteName, CancellationToken ct)
    {
        var cmd = $"Import-Module WebAdministration; " +
                  $"Get-WebBinding -Name '{Escape(siteName)}' " +
                  "| Select-Object protocol,bindingInformation " +
                  "| ConvertTo-Json -Compress";

        var result = await RunCommandAsync(cmd, ct);
        if (result.ExitCode != 0 || !string.IsNullOrWhiteSpace(result.Stderr))
            return Array.Empty<IisBindingDto>();

        var output = result.Stdout.Trim();
        if (string.IsNullOrWhiteSpace(output))
            return Array.Empty<IisBindingDto>();

        return ParseBindingsJson(output);
    }

    public async Task<CommandResult> SetBindingAsync(string siteName, IisBindingUpdateRequest request, CancellationToken ct)
    {
        var protocol = request.Protocol?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(protocol))
            return new CommandResult(1, string.Empty, "Missing protocol");

        if (!string.Equals(protocol, "http", StringComparison.OrdinalIgnoreCase))
            return new CommandResult(1, string.Empty, "Only http protocol is supported via Ops");

        if (request.Port is <= 0 or > 65535)
            return new CommandResult(1, string.Empty, "Invalid port");

        var ip = string.IsNullOrWhiteSpace(request.IpAddress) ? "*" : request.IpAddress.Trim();
        var host = request.Host?.Trim() ?? string.Empty;

        var replaceCmd = request.ReplaceExisting
            ? $"Get-WebBinding -Name '{Escape(siteName)}' -Protocol '{Escape(protocol)}' | Remove-WebBinding -Confirm:$false; "
            : string.Empty;

        var cmd = "Import-Module WebAdministration; " +
                  replaceCmd +
                  $"New-WebBinding -Name '{Escape(siteName)}' " +
                  $"-Protocol '{Escape(protocol)}' " +
                  $"-Port {request.Port} " +
                  $"-IPAddress '{Escape(ip)}' " +
                  $"-HostHeader '{Escape(host)}'";

        return await RunCommandAsync(cmd, ct);
    }

    public static (string ip, int port, string host) ParseBindingInformation(string bindingInformation)
    {
        if (string.IsNullOrWhiteSpace(bindingInformation))
            return ("*", 0, string.Empty);

        var parts = bindingInformation.Split(':', 3);
        var ip = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0] : "*";
        var port = parts.Length > 1 && int.TryParse(parts[1], out var parsed) ? parsed : 0;
        var host = parts.Length > 2 ? parts[2] : string.Empty;

        return (ip, port, host);
    }

    public static string BuildBindingInformation(string ip, int port, string host)
        => $"{(string.IsNullOrWhiteSpace(ip) ? "*" : ip)}:{port}:{host ?? string.Empty}";

    public async Task<string?> GetSiteAppPoolAsync(string siteName, CancellationToken ct)
    {
        var cmd = $"Import-Module WebAdministration; (Get-ItemProperty 'IIS:\\Sites\\{Escape(siteName)}').applicationPool";
        var result = await RunCommandAsync(cmd, ct);
        if (result.ExitCode != 0)
            return null;

        return result.Stdout.Trim();
    }

    public async Task<AppPoolStatusDto> GetAppPoolStatusAsync(string appPoolName, CancellationToken ct)
    {
        var cmd = $"Import-Module WebAdministration; (Get-WebAppPoolState -Name '{Escape(appPoolName)}').Value";
        var result = await RunCommandAsync(cmd, ct);
        var status = result.ExitCode != 0
            ? "unknown"
            : result.Stdout.Trim().ToLowerInvariant();
        return new AppPoolStatusDto(appPoolName, status);
    }

    public Task<CommandResult> StartAppPoolAsync(string appPoolName, CancellationToken ct)
        => RunCommandAsync($"Import-Module WebAdministration; Start-WebAppPool -Name '{Escape(appPoolName)}'", ct);

    public Task<CommandResult> StopAppPoolAsync(string appPoolName, CancellationToken ct)
        => RunCommandAsync($"Import-Module WebAdministration; Stop-WebAppPool -Name '{Escape(appPoolName)}'", ct);

    public Task<CommandResult> RecycleAppPoolAsync(string appPoolName, CancellationToken ct)
        => RunCommandAsync($"Import-Module WebAdministration; Restart-WebAppPool -Name '{Escape(appPoolName)}'", ct);

    public async Task<CompressionSettingsDto> GetCompressionSettingsAsync(CancellationToken ct)
    {
        var cmd = "Import-Module WebAdministration; " +
                  "$static=(Get-WebConfigurationProperty -Filter /system.webServer/urlCompression -Name doStaticCompression).Value; " +
                  "$dynamic=(Get-WebConfigurationProperty -Filter /system.webServer/urlCompression -Name doDynamicCompression).Value; " +
                  "@{static=$static;dynamic=$dynamic} | ConvertTo-Json -Compress";

        var result = await RunCommandAsync(cmd, ct);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Stdout))
            return new CompressionSettingsDto(false, false);

        return CompressionSettingsParser.Parse(result.Stdout);
    }

    public Task<CommandResult> SetCompressionSettingsAsync(CompressionSettingsDto settings, CancellationToken ct)
    {
        var staticValue = settings.StaticEnabled ? "$true" : "$false";
        var dynamicValue = settings.DynamicEnabled ? "$true" : "$false";
        var cmd = "Import-Module WebAdministration; " +
                  $"Set-WebConfigurationProperty -Filter /system.webServer/urlCompression -Name doStaticCompression -Value {staticValue}; " +
                  $"Set-WebConfigurationProperty -Filter /system.webServer/urlCompression -Name doDynamicCompression -Value {dynamicValue}";
        return RunCommandAsync(cmd, ct);
    }

    public static CommandResult ClearCompressionCache()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp", "IIS Temporary Compressed Files");
            if (!Directory.Exists(path))
                return new CommandResult(0, "Cache folder not found", string.Empty);

            Directory.Delete(path, true);
            Directory.CreateDirectory(path);
            return new CommandResult(0, "Compression cache cleared", string.Empty);
        }
        catch (Exception ex)
        {
            return new CommandResult(1, string.Empty, ex.Message);
        }
    }

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

        using var proc = Process.Start(psi);
        if (proc is null)
            return new ServiceStatusDto(name, "error", "Failed to start PowerShell");

        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (!string.IsNullOrWhiteSpace(stderr))
            return new ServiceStatusDto(name, "error", stderr.Trim());

        var output = stdout.Trim();
        if (outputOnly)
            return new ServiceStatusDto(name, output.ToLowerInvariant());

        return new ServiceStatusDto(name, "ok", output);
    }

    private static async Task<CommandResult> RunCommandAsync(string cmd, CancellationToken ct)
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

        using var proc = Process.Start(psi);
        if (proc is null)
            return new CommandResult(1, string.Empty, "Failed to start PowerShell");

        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        return new CommandResult(proc.ExitCode, stdout.Trim(), stderr.Trim());
    }

    private static string Escape(string value)
        => value.Replace("'", "''");

    private static IisBindingDto[] ParseBindingsJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<IisBindingDto>();

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
                AddBinding(list, item);
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            AddBinding(list, doc.RootElement);
        }

        return list.ToArray();
    }

    private static void AddBinding(List<IisBindingDto> list, JsonElement element)
    {
        if (!element.TryGetProperty("protocol", out var protocolEl) ||
            !element.TryGetProperty("bindingInformation", out var bindingEl))
            return;

        var protocol = protocolEl.GetString() ?? string.Empty;
        var bindingInfo = bindingEl.GetString() ?? string.Empty;
        var (ip, port, host) = ParseBindingInformation(bindingInfo);
        list.Add(new IisBindingDto(protocol, ip, port, host, bindingInfo));
    }
}
