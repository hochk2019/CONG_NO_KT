using System.Diagnostics;
using System.Collections.Generic;

namespace Ops.Agent.Services;

public sealed record DbConnectionInfo(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password);

public sealed class BackupRunner
{
    public static string BuildDumpArgs(string filePath, DbConnectionInfo info)
    {
        var args = new List<string> { "-F", "c", "-f", Quote(filePath) };
        AppendConnectionArgs(args, info);
        args.Add(Quote(info.Database));
        return string.Join(" ", args);
    }

    public static string BuildRestoreArgs(string filePath, DbConnectionInfo info)
    {
        var args = new List<string>
        {
            "--clean",
            "--if-exists",
            "--no-owner",
            "--no-privileges"
        };

        AppendConnectionArgs(args, info);
        args.Add("-d");
        args.Add(Quote(info.Database));
        args.Add(Quote(filePath));
        return string.Join(" ", args);
    }

    public static string ParseDatabaseName(string connectionString)
        => ParseConnectionInfo(connectionString).Database;

    public static DbConnectionInfo ParseConnectionInfo(string connectionString)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var idx = part.IndexOf('=');
            if (idx <= 0)
                continue;

            var key = part[..idx].Trim();
            var value = part[(idx + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                values[key] = value;
        }

        var host = GetValue(values, "Host", "Server", "Data Source") ?? "localhost";
        var db = GetValue(values, "Database", "Initial Catalog") ?? "postgres";
        var user = GetValue(values, "Username", "User ID", "User Id", "UID") ?? string.Empty;
        var password = GetValue(values, "Password", "Pwd") ?? string.Empty;
        var portText = GetValue(values, "Port");
        var port = int.TryParse(portText, out var parsed) ? parsed : 5432;

        return new DbConnectionInfo(host, port, db, user, password);
    }

    public static string ResolvePgBinPath(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return configured;

        var candidates = new[]
        {
            @"C:\Program Files\PostgreSQL\16\bin",
            @"C:\Program Files\PostgreSQL\15\bin",
            @"C:\Program Files\PostgreSQL\14\bin"
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public async Task<CommandResult> RunAsync(string exePath, string args, string connectionString, CancellationToken ct)
    {
        if (!File.Exists(exePath))
            return new CommandResult(1, string.Empty, $"Executable not found: {exePath}");

        var info = ParseConnectionInfo(connectionString);
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ApplyConnectionEnv(psi, info);

        using var proc = Process.Start(psi);
        if (proc is null)
            return new CommandResult(1, string.Empty, "Failed to start process");

        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        return new CommandResult(proc.ExitCode, stdout, stderr);
    }

    private static void ApplyConnectionEnv(ProcessStartInfo psi, DbConnectionInfo info)
    {
        if (!string.IsNullOrWhiteSpace(info.Host))
            psi.Environment["PGHOST"] = info.Host;
        if (info.Port > 0)
            psi.Environment["PGPORT"] = info.Port.ToString();
        if (!string.IsNullOrWhiteSpace(info.Username))
            psi.Environment["PGUSER"] = info.Username;
        if (!string.IsNullOrWhiteSpace(info.Password))
            psi.Environment["PGPASSWORD"] = info.Password;
    }

    private static void AppendConnectionArgs(List<string> args, DbConnectionInfo info)
    {
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
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "\"\"";

        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private static string? GetValue(Dictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
