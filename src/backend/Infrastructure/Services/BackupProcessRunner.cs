using System.Diagnostics;

namespace CongNoGolden.Infrastructure.Services;

public sealed record BackupProcessResult(int ExitCode, string Stdout, string Stderr);

public sealed class BackupProcessRunner
{
    public async Task<BackupProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken ct)
    {
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new BackupProcessResult(process.ExitCode, stdout, stderr);
    }
}
