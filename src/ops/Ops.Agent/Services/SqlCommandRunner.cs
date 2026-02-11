namespace Ops.Agent.Services;

public sealed class SqlCommandRunner(BackupRunner runner) : ISqlCommandRunner
{
    public Task<CommandResult> RunAsync(string exePath, string args, string connectionString, CancellationToken ct)
        => runner.RunAsync(exePath, args, connectionString, ct);
}
