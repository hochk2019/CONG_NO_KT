namespace Ops.Shared.Models;

public sealed record SqlExecuteRequest(string Sql);

public sealed record SqlExecuteResponse(int ExitCode, string Stdout, string Stderr, int? RowsAffected);
