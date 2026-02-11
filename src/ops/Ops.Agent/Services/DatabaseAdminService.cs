using DbUp;
using Ops.Shared.Config;

namespace Ops.Agent.Services;

public sealed class DatabaseAdminService(ISqlCommandRunner runner)
{
    public async Task<CommandResult> CreateDatabaseAsync(OpsConfig config, CancellationToken ct)
    {
        var info = BackupRunner.ParseConnectionInfo(config.Database.ConnectionString);
        if (string.IsNullOrWhiteSpace(info.Database))
            return new CommandResult(1, string.Empty, "Database name is missing");

        var pgBin = BackupRunner.ResolvePgBinPath(config.Database.PgBinPath);
        if (string.IsNullOrWhiteSpace(pgBin))
            return new CommandResult(1, string.Empty, "Không tìm thấy thư mục pg_bin");

        var exe = Path.Combine(pgBin, "psql.exe");
        var adminInfo = info with { Database = "postgres" };
        var sql = BuildCreateDatabaseSql(info.Database, info.Username);
        var args = SqlConsoleService.BuildArgs(sql, adminInfo);

        var result = await runner.RunAsync(exe, args, config.Database.ConnectionString, ct);
        if (result.ExitCode == 0)
        {
            var normalized = result.Stdout.Trim();
            if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "DO", StringComparison.OrdinalIgnoreCase))
                return new CommandResult(0, "Đã tạo database (hoặc đã tồn tại)", string.Empty);
        }

        return result;
    }

    public Task<CommandResult> RunMigrationsAsync(OpsConfig config, CancellationToken ct)
        => Task.FromResult(RunMigrations(config));

    private static CommandResult RunMigrations(OpsConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Database.ConnectionString))
            return new CommandResult(1, string.Empty, "Thiếu connection string");

        var scriptsPath = ResolveScriptsPath(config);
        if (string.IsNullOrWhiteSpace(scriptsPath) || !Directory.Exists(scriptsPath))
            return new CommandResult(1, string.Empty, $"Không tìm thấy thư mục migrations: {scriptsPath}");

        try
        {
            var upgrader = DeployChanges.To
                .PostgresqlDatabase(config.Database.ConnectionString)
                .WithScriptsFromFileSystem(scriptsPath)
                .LogToAutodetectedLog()
                .Build();

            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
                return new CommandResult(1, string.Empty, result.Error?.Message ?? "Migration failed");

            return new CommandResult(0, "Đã chạy migrations", string.Empty);
        }
        catch (Exception ex)
        {
            return new CommandResult(1, string.Empty, ex.Message);
        }
    }

    public static string ResolveScriptsPath(OpsConfig config)
    {
        var backendPath = ResolveScriptsPath(config.Backend.AppPath);
        if (!string.IsNullOrWhiteSpace(backendPath))
            return backendPath;

        var repoPath = ResolveScriptsPath(config.Updates.RepoPath);
        if (!string.IsNullOrWhiteSpace(repoPath))
            return repoPath;

        return Path.Combine(config.Backend.AppPath, "scripts", "db", "migrations");
    }

    private static string? ResolveScriptsPath(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return null;

        var migrations = Path.Combine(root, "scripts", "db", "migrations");
        if (Directory.Exists(migrations))
            return migrations;

        var migration = Path.Combine(root, "scripts", "db", "migration");
        if (Directory.Exists(migration))
            return migration;

        return null;
    }

    public static string BuildCreateDatabaseSql(string databaseName, string owner)
    {
        var safeDb = EscapeIdentifier(databaseName);
        var ownerClause = string.IsNullOrWhiteSpace(owner)
            ? string.Empty
            : $" OWNER {EscapeIdentifier(owner)}";

        return $"""
               DO $$
               BEGIN
                 IF NOT EXISTS (SELECT FROM pg_database WHERE datname = '{EscapeLiteral(databaseName)}') THEN
                   CREATE DATABASE {safeDb}{ownerClause};
                 END IF;
               END $$;
               """;
    }

    private static string EscapeIdentifier(string value)
        => $"\"{value.Replace("\"", "\"\"")}\"";

    private static string EscapeLiteral(string value)
        => value.Replace("'", "''");
}
