using Ops.Shared.Config;

namespace Ops.Agent.Services;

public sealed class BackupService(BackupRunner runner)
{
    public async Task<(string file, CommandResult result)> CreateBackupAsync(OpsConfig config, CancellationToken ct)
    {
        Directory.CreateDirectory(config.Paths.BackupRoot);
        var file = BuildBackupPath(config, DateTimeOffset.UtcNow);
        var connInfo = BackupRunner.ParseConnectionInfo(config.Database.ConnectionString);
        var pgBin = BackupRunner.ResolvePgBinPath(config.Database.PgBinPath);
        var exe = Path.Combine(pgBin, "pg_dump.exe");
        var args = BackupRunner.BuildDumpArgs(file, connInfo);

        var result = await runner.RunAsync(exe, args, config.Database.ConnectionString, ct);
        CleanupOldBackups(config, ResolveRetention(config));
        return (file, result);
    }

    public static string BuildBackupPath(OpsConfig config, DateTimeOffset now)
        => Path.Combine(config.Paths.BackupRoot, $"congno_{now:yyyyMMdd_HHmmss}.dump");

    public static int ResolveRetention(OpsConfig config)
    {
        var scheduleRetention = config.BackupSchedule.RetentionCount;
        if (scheduleRetention > 0)
            return scheduleRetention;

        return config.Database.RetentionCount > 0 ? config.Database.RetentionCount : 0;
    }

    public static void CleanupOldBackups(OpsConfig config, int retentionCount)
    {
        if (retentionCount <= 0 || !Directory.Exists(config.Paths.BackupRoot))
            return;

        var files = Directory.GetFiles(config.Paths.BackupRoot, "*.dump", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetCreationTimeUtc)
            .ToArray();

        foreach (var file in files.Skip(retentionCount))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }
}
