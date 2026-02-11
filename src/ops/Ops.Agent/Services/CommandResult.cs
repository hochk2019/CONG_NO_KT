namespace Ops.Agent.Services;

public sealed record CommandResult(
    int ExitCode,
    string Stdout,
    string Stderr);
