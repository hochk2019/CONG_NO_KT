using Ops.Shared.Models;

namespace Ops.Agent.Services;

public sealed class ServiceConfigControl(ProcessRunner runner)
{
    public async Task<ServiceConfigDto> GetConfigAsync(string serviceName, CancellationToken ct)
    {
        var result = await runner.RunAsync("sc.exe", $"qc \"{serviceName}\"", null, ct);
        if (result.ExitCode != 0)
            return new ServiceConfigDto(serviceName, "unknown", string.Empty, null);

        var lines = result.Stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var startMode = ExtractValue(lines, "START_TYPE");
        var account = ExtractValue(lines, "SERVICE_START_NAME");
        var displayName = ExtractValue(lines, "DISPLAY_NAME");
        return new ServiceConfigDto(serviceName, NormalizeStartMode(startMode), account, displayName);
    }

    public async Task<CommandResult> UpdateConfigAsync(ServiceConfigUpdateRequest request, CancellationToken ct)
    {
        var mode = request.StartMode?.Trim().ToLowerInvariant();
        var scMode = mode switch
        {
            "auto" => "auto",
            "manual" => "demand",
            "disabled" => "disabled",
            _ => "auto"
        };

        var args = $"config \"{request.Name}\" start= {scMode}";
        if (!string.IsNullOrWhiteSpace(request.ServiceAccount))
        {
            var account = request.ServiceAccount.Trim();
            var password = request.ServicePassword?.Trim() ?? string.Empty;
            args += $" obj= \"{account}\" password= \"{password}\"";
        }

        return await runner.RunAsync("sc.exe", args, null, ct);
    }

    public async Task<ServiceRecoveryDto> GetRecoveryAsync(string serviceName, CancellationToken ct)
    {
        var result = await runner.RunAsync("sc.exe", $"qfailure \"{serviceName}\"", null, ct);
        if (result.ExitCode != 0)
            return new ServiceRecoveryDto(serviceName, "unknown", "unknown", "unknown", 0);

        var lines = result.Stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var resetLine = lines.FirstOrDefault(l => l.Contains("RESET_PERIOD", StringComparison.OrdinalIgnoreCase));
        var resetSeconds = ParseResetSeconds(resetLine);

        var actions = ParseFailureActions(lines);
        return new ServiceRecoveryDto(
            serviceName,
            actions.ElementAtOrDefault(0) ?? "none",
            actions.ElementAtOrDefault(1) ?? "none",
            actions.ElementAtOrDefault(2) ?? "none",
            resetSeconds / 60);
    }

    public async Task<CommandResult> UpdateRecoveryAsync(ServiceRecoveryUpdateRequest request, CancellationToken ct)
    {
        var resetSeconds = Math.Max(request.ResetPeriodMinutes, 0) * 60;
        var actions = string.Join('/',
            MapFailureAction(request.FirstFailure), 60000,
            MapFailureAction(request.SecondFailure), 60000,
            MapFailureAction(request.SubsequentFailure), 0);

        var args = $"failure \"{request.Name}\" reset= {resetSeconds} actions= {actions}";
        return await runner.RunAsync("sc.exe", args, null, ct);
    }

    private static string ExtractValue(IEnumerable<string> lines, string key)
    {
        var line = lines.FirstOrDefault(l => l.TrimStart().StartsWith(key, StringComparison.OrdinalIgnoreCase));
        if (line is null)
            return string.Empty;

        var parts = line.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1].Trim() : string.Empty;
    }

    private static string NormalizeStartMode(string raw)
    {
        if (raw.Contains("AUTO", StringComparison.OrdinalIgnoreCase))
            return "auto";
        if (raw.Contains("DISABLED", StringComparison.OrdinalIgnoreCase))
            return "disabled";
        if (raw.Contains("DEMAND", StringComparison.OrdinalIgnoreCase))
            return "manual";
        return "unknown";
    }

    private static int ParseResetSeconds(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return 0;

        var parts = line.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return 0;

        return int.TryParse(parts[1].Trim(), out var seconds) ? seconds : 0;
    }

    private static List<string> ParseFailureActions(string[] lines)
    {
        var actions = new List<string>();
        var index = Array.FindIndex(lines, l => l.Contains("FAILURE_ACTIONS", StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return actions;

        for (var i = index; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.Contains("RESTART", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("NONE", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("RUN", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            actions.Add(NormalizeFailureAction(line));
        }

        return actions;
    }

    private static string NormalizeFailureAction(string line)
    {
        if (line.Contains("RESTART", StringComparison.OrdinalIgnoreCase))
            return "restart";
        if (line.Contains("RUN", StringComparison.OrdinalIgnoreCase))
            return "run";
        return "none";
    }

    private static string MapFailureAction(string action)
    {
        return action?.Trim().ToLowerInvariant() switch
        {
            "restart" => "restart",
            "run" => "run",
            _ => "none"
        };
    }
}
