namespace Ops.Agent.Services;

public interface ISqlCommandRunner
{
    Task<CommandResult> RunAsync(string exePath, string args, string connectionString, CancellationToken ct);
}
