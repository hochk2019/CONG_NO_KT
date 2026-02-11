using CongNoGolden.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CongNoGolden.Tests.Integration;

public sealed class TestDatabaseFixture : IAsyncLifetime
{
    private const string DefaultAdminConnection =
        "Host=localhost;Port=5432;Username=postgres;Password=Postgres@123;Database=postgres;Pooling=false";

    private readonly string _adminConnectionString;

    public TestDatabaseFixture()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable("CONGNO_TEST_DB_ADMIN")
            ?? DefaultAdminConnection;
    }

    public string DatabaseName { get; } = $"congno_test_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = string.Empty;

    public DbContextOptions<ConGNoDbContext> Options { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        var adminBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString);
        if (string.IsNullOrWhiteSpace(adminBuilder.Database))
        {
            adminBuilder.Database = "postgres";
        }
        adminBuilder.Pooling = false;

        await using (var connection = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{DatabaseName}\";";
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == "42P04")
            {
            }
        }

        var appBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString)
        {
            Database = DatabaseName,
            Pooling = false
        };

        ConnectionString = appBuilder.ConnectionString;
        var optionsBuilder = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention();

        Options = optionsBuilder.Options;

        await using var db = new ConGNoDbContext(Options);
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS congno;");
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        var adminBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString);
        if (string.IsNullOrWhiteSpace(adminBuilder.Database))
        {
            adminBuilder.Database = "postgres";
        }
        adminBuilder.Pooling = false;

        await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
        await connection.OpenAsync();

        await using (var terminate = connection.CreateCommand())
        {
            terminate.CommandText =
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name;";
            terminate.Parameters.AddWithValue("name", DatabaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\";";
            await drop.ExecuteNonQueryAsync();
        }
    }

    public ConGNoDbContext CreateContext()
    {
        return new ConGNoDbContext(Options);
    }
}
