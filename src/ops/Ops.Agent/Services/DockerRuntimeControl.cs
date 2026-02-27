using Ops.Shared.Config;
using Ops.Shared.Models;

namespace Ops.Agent.Services;

public sealed class DockerRuntimeControl
{
    private readonly Func<string, string, string, CancellationToken, Task<CommandResult>> _runProcess;

    public DockerRuntimeControl(ProcessRunner runner)
        : this((file, args, workdir, ct) => runner.RunAsync(file, args, workdir, ct))
    {
    }

    public DockerRuntimeControl(Func<string, string, string, CancellationToken, Task<CommandResult>> runProcess)
    {
        _runProcess = runProcess;
    }

    public async Task<ServiceStatusDto> GetServiceStatusAsync(OpsConfig config, string serviceName, CancellationToken ct)
    {
        if (!TryValidateDockerConfig(config, serviceName, out var docker, out var validationError))
        {
            return new ServiceStatusDto(serviceName, "error", validationError);
        }

        var result = await RunComposeAsync(docker, $"ps --services --status running {serviceName}", ct);
        if (result.ExitCode != 0)
        {
            return new ServiceStatusDto(serviceName, "error", NormalizeError(result));
        }

        var runningServices = SplitLines(result.Stdout);
        var isRunning = runningServices.Any(x => string.Equals(x, serviceName, StringComparison.OrdinalIgnoreCase));
        return new ServiceStatusDto(serviceName, isRunning ? "running" : "stopped");
    }

    public async Task<ServiceStatusDto> StartServiceAsync(OpsConfig config, string serviceName, CancellationToken ct)
    {
        if (!TryValidateDockerConfig(config, serviceName, out var docker, out var validationError))
        {
            return new ServiceStatusDto(serviceName, "error", validationError);
        }

        var result = await RunComposeAsync(docker, $"up -d {serviceName}", ct);
        if (result.ExitCode != 0)
        {
            return new ServiceStatusDto(serviceName, "error", NormalizeError(result));
        }

        return await GetServiceStatusAsync(config, serviceName, ct);
    }

    public async Task<ServiceStatusDto> StopServiceAsync(OpsConfig config, string serviceName, CancellationToken ct)
    {
        if (!TryValidateDockerConfig(config, serviceName, out var docker, out var validationError))
        {
            return new ServiceStatusDto(serviceName, "error", validationError);
        }

        var result = await RunComposeAsync(docker, $"stop {serviceName}", ct);
        if (result.ExitCode != 0)
        {
            return new ServiceStatusDto(serviceName, "error", NormalizeError(result));
        }

        return await GetServiceStatusAsync(config, serviceName, ct);
    }

    public async Task<ServiceStatusDto> RestartServiceAsync(OpsConfig config, string serviceName, CancellationToken ct)
    {
        if (!TryValidateDockerConfig(config, serviceName, out var docker, out var validationError))
        {
            return new ServiceStatusDto(serviceName, "error", validationError);
        }

        var result = await RunComposeAsync(docker, $"restart {serviceName}", ct);
        if (result.ExitCode != 0)
        {
            return new ServiceStatusDto(serviceName, "error", NormalizeError(result));
        }

        return await GetServiceStatusAsync(config, serviceName, ct);
    }

    private static bool TryValidateDockerConfig(
        OpsConfig config,
        string serviceName,
        out DockerRuntimeConfig docker,
        out string error)
    {
        docker = config.Runtime.Docker;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            error = "Docker service name is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(docker.ComposeFilePath))
        {
            error = "Docker compose file path is missing";
            return false;
        }

        if (!File.Exists(docker.ComposeFilePath))
        {
            error = $"Docker compose file not found: {docker.ComposeFilePath}";
            return false;
        }

        return true;
    }

    private async Task<CommandResult> RunComposeAsync(DockerRuntimeConfig docker, string command, CancellationToken ct)
    {
        var workingDirectory = ResolveWorkingDirectory(docker);
        var args = BuildComposeArgs(docker, command);
        return await _runProcess("docker", args, workingDirectory, ct);
    }

    private static string BuildComposeArgs(DockerRuntimeConfig docker, string command)
    {
        var args = $"compose -f \"{docker.ComposeFilePath}\"";
        if (!string.IsNullOrWhiteSpace(docker.ProjectName))
        {
            args += $" -p \"{docker.ProjectName}\"";
        }

        return $"{args} {command}".Trim();
    }

    private static string ResolveWorkingDirectory(DockerRuntimeConfig docker)
    {
        if (!string.IsNullOrWhiteSpace(docker.WorkingDirectory))
        {
            return docker.WorkingDirectory;
        }

        var fromCompose = Path.GetDirectoryName(docker.ComposeFilePath);
        return string.IsNullOrWhiteSpace(fromCompose) ? AppContext.BaseDirectory : fromCompose;
    }

    private static string NormalizeError(CommandResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            return result.Stderr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(result.Stdout))
        {
            return result.Stdout.Trim();
        }

        return "Docker command failed";
    }

    private static IReadOnlyList<string> SplitLines(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<string>();
        }

        return output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToArray();
    }
}
