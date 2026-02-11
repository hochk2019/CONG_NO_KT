using System.Data.Common;
using CongNoGolden.Application.Common.Interfaces;
using Npgsql;

namespace CongNoGolden.Infrastructure.Data;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DbConnection Create()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
