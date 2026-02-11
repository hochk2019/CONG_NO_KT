using Ops.Shared.Config;
using Ops.Shared.Models;

namespace Ops.Agent.Services;

public sealed class SqlConsoleService(ISqlCommandRunner runner)
{
    public async Task<SqlExecuteResponse> ExecuteAsync(OpsConfig config, string sql, bool preview, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new SqlExecuteResponse(1, string.Empty, "SQL is empty", null);

        var connInfo = BackupRunner.ParseConnectionInfo(config.Database.ConnectionString);
        var pgBin = BackupRunner.ResolvePgBinPath(config.Database.PgBinPath);
        var exe = Path.Combine(pgBin, "psql.exe");
        var commandSql = preview ? BuildPreviewSql(sql) : sql.Trim();
        var args = BuildArgs(commandSql, connInfo);

        var result = await runner.RunAsync(exe, args, config.Database.ConnectionString, ct);
        var rows = TryParseRowsAffected(result.Stdout);
        return new SqlExecuteResponse(result.ExitCode, result.Stdout, result.Stderr, rows);
    }

    public static string BuildArgs(string sql, DbConnectionInfo info)
    {
        var args = new List<string>
        {
            "-X",
            "-v", "ON_ERROR_STOP=1",
            "-P", "pager=off",
            "-P", "footer=on"
        };

        if (!string.IsNullOrWhiteSpace(info.Host))
        {
            args.Add("-h");
            args.Add(Quote(info.Host));
        }

        if (info.Port > 0)
        {
            args.Add("-p");
            args.Add(info.Port.ToString());
        }

        if (!string.IsNullOrWhiteSpace(info.Username))
        {
            args.Add("-U");
            args.Add(Quote(info.Username));
        }

        if (!string.IsNullOrWhiteSpace(info.Database))
        {
            args.Add("-d");
            args.Add(Quote(info.Database));
        }

        args.Add("-c");
        args.Add(Quote(sql));

        return string.Join(" ", args);
    }

    public static string BuildPreviewSql(string sql)
    {
        var trimmed = TrimTrailingSemicolon(sql);
        if (IsSelectLike(trimmed))
            return $"SELECT * FROM ({trimmed}) AS preview LIMIT 50;";

        return $"BEGIN; {trimmed}; ROLLBACK;";
    }

    public static int? TryParseRowsAffected(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
            return null;

        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (TryParseStandardRowCount(line, out var count))
                return count;
        }

        return null;
    }

    private static bool TryParseStandardRowCount(string line, out int count)
    {
        count = 0;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && IsRowCountVerb(parts[0]) && int.TryParse(parts[1], out count))
            return true;

        if (parts.Length == 3 && string.Equals(parts[0], "INSERT", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(parts[2], out count))
            return true;

        return false;
    }

    private static bool IsRowCountVerb(string verb)
        => verb.Equals("UPDATE", StringComparison.OrdinalIgnoreCase)
        || verb.Equals("DELETE", StringComparison.OrdinalIgnoreCase)
        || verb.Equals("SELECT", StringComparison.OrdinalIgnoreCase)
        || verb.Equals("MOVE", StringComparison.OrdinalIgnoreCase)
        || verb.Equals("FETCH", StringComparison.OrdinalIgnoreCase)
        || verb.Equals("COPY", StringComparison.OrdinalIgnoreCase);

    private static bool IsSelectLike(string sql)
    {
        var trimmed = sql.TrimStart();
        return trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimTrailingSemicolon(string sql)
    {
        var trimmed = sql.Trim();
        if (trimmed.EndsWith(';'))
            trimmed = trimmed[..^1];

        return trimmed.Trim();
    }

    private static string Quote(string value)
    {
        var escaped = value.Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
}
