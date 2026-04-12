namespace CongNoGolden.Application.Backups;

public sealed record BackupProcessResult(int ExitCode, string Stdout, string Stderr);
