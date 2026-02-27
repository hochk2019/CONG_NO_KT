using CongNoGolden.Infrastructure.Data;
using Npgsql;
using Xunit;

namespace Tests.Unit;

public sealed class NpgsqlConnectionFactoryTests
{
    [Fact]
    public void CreateRead_UsesReadReplicaConnectionString_WhenConfigured()
    {
        var factory = new NpgsqlConnectionFactory(
            "Host=write-db;Port=5432;Database=main;Username=app;Password=pw",
            "Host=read-db;Port=5432;Database=main;Username=app;Password=pw");

        using var readConnection = factory.CreateRead();

        var npgsql = Assert.IsType<NpgsqlConnection>(readConnection);
        Assert.Contains("Host=read-db", npgsql.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateRead_FallsBackToWriteConnectionString_WhenReadReplicaMissing()
    {
        var factory = new NpgsqlConnectionFactory(
            "Host=write-db;Port=5432;Database=main;Username=app;Password=pw",
            readConnectionString: null);

        using var readConnection = factory.CreateRead();
        var npgsql = Assert.IsType<NpgsqlConnection>(readConnection);

        Assert.Contains("Host=write-db", npgsql.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }
}
