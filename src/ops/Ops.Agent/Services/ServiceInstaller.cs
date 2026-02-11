using Ops.Shared.Config;
using System.Collections.Generic;

namespace Ops.Agent.Services;

public sealed class ServiceInstaller(ProcessRunner runner)
{
    public static string ResolveBackendExePath(OpsConfig config, string? exePath)
    {
        if (!string.IsNullOrWhiteSpace(exePath))
            return exePath;

        return Path.Combine(config.Backend.AppPath, config.Backend.ExeName);
    }

    public async Task<CommandResult> InstallBackendAsync(OpsConfig config, string? exePath, CancellationToken ct)
    {
        var serviceName = config.Backend.ServiceName;
        var resolvedExe = ResolveBackendExePath(config, exePath);
        if (!File.Exists(resolvedExe))
            return new CommandResult(1, string.Empty, $"Backend exe not found: {resolvedExe}");

        var baseUrl = config.Backend.BaseUrl;
        var urlsArg = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : $"--urls \"{baseUrl}\"";

        var nssm = config.Updates.NssmPath;
        if (!File.Exists(nssm))
        {
            var appDirFallback = Path.GetDirectoryName(resolvedExe) ?? config.Backend.AppPath;
            var contentRootArg = $"--contentRoot \"{appDirFallback}\"";
            var binPath = $"\\\"{resolvedExe}\\\" {contentRootArg} {urlsArg}".Trim();
            var args = $"create {serviceName} binPath= \"{binPath}\" start= auto";
            return await runner.RunAsync("sc.exe", args, Environment.SystemDirectory, ct);
        }

        var nssmDir = Path.GetDirectoryName(nssm) ?? Environment.CurrentDirectory;
        var install = await runner.RunAsync(nssm, $"install {serviceName} \"{resolvedExe}\"", nssmDir, ct);
        if (install.ExitCode != 0)
            return install;

        var appDir = Path.GetDirectoryName(resolvedExe) ?? config.Backend.AppPath;
        var setDir = await runner.RunAsync(nssm, $"set {serviceName} AppDirectory \"{appDir}\"", nssmDir, ct);
        if (setDir.ExitCode != 0)
            return setDir;

        var envPairs = new List<string> { "ASPNETCORE_ENVIRONMENT=Production" };
        if (!string.IsNullOrWhiteSpace(baseUrl))
            envPairs.Add($"ASPNETCORE_URLS={baseUrl}");
        var envValue = string.Join(" ", envPairs);

        var setEnv = await runner.RunAsync(nssm, $"set {serviceName} AppEnvironmentExtra \"{envValue}\"", nssmDir, ct);
        if (setEnv.ExitCode != 0)
            return setEnv;

        return install;
    }
}
