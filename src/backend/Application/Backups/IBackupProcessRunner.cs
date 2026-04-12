using System.Diagnostics;

namespace CongNoGolden.Application.Backups;

public interface IBackupProcessRunner
{
    Task<BackupProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken ct);
}
