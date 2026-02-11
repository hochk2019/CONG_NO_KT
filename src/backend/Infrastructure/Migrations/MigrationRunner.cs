using DbUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CongNoGolden.Infrastructure.Migrations;

public static class MigrationRunner
{
    public static void ApplyMigrations(IConfiguration configuration, ILogger logger)
    {
        var options = LoadOptions(configuration);
        if (!options.Enabled)
        {
            logger.LogInformation("Migrations disabled.");
            return;
        }

        var (connectionString, source) = ResolveConnectionString(configuration, options);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Migration connection string is not configured.");
        }
        logger.LogInformation("Using migration connection string from {Source}.", source);

        var scriptsPath = ResolveScriptsPath(options);
        if (!Directory.Exists(scriptsPath))
        {
            logger.LogError("Migration scripts directory not found: {Path}", scriptsPath);
            throw new DirectoryNotFoundException($"Migration scripts directory not found: {scriptsPath}");
        }

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsFromFileSystem(scriptsPath)
            .LogToAutodetectedLog()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            logger.LogError(result.Error, "Database migration failed.");
            throw result.Error;
        }

        logger.LogInformation("Database migrations applied.");
    }

    private static MigrationOptions LoadOptions(IConfiguration configuration)
    {
        var enabledValue = configuration["Migrations:Enabled"];
        var enabled = true;
        if (!string.IsNullOrWhiteSpace(enabledValue) && bool.TryParse(enabledValue, out var parsed))
        {
            enabled = parsed;
        }

        return new MigrationOptions
        {
            Enabled = enabled,
            ScriptsPath = configuration["Migrations:ScriptsPath"],
            ConnectionString = configuration["Migrations:ConnectionString"]
        };
    }

    private static (string? ConnectionString, string Source) ResolveConnectionString(
        IConfiguration configuration,
        MigrationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return (options.ConnectionString, "Migrations:ConnectionString");
        }

        var migrationsConnection = configuration.GetConnectionString("Migrations");
        if (!string.IsNullOrWhiteSpace(migrationsConnection))
        {
            return (migrationsConnection, "ConnectionStrings:Migrations");
        }

        var defaultConnection = configuration.GetConnectionString("Default");
        return (defaultConnection, "ConnectionStrings:Default");
    }

    private static string ResolveScriptsPath(MigrationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ScriptsPath))
        {
            if (Path.IsPathRooted(options.ScriptsPath))
            {
                return options.ScriptsPath!;
            }

            var current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, options.ScriptsPath);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            return Path.Combine(Directory.GetCurrentDirectory(), options.ScriptsPath);
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "scripts", "db", "migrations");
    }
}
