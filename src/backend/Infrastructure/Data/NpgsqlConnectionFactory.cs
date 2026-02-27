using System.Data.Common;
using CongNoGolden.Application.Common.Interfaces;
using Npgsql;

namespace CongNoGolden.Infrastructure.Data;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _writeConnectionString;
    private readonly string? _readConnectionString;

    public NpgsqlConnectionFactory(string writeConnectionString, string? readConnectionString = null)
    {
        _writeConnectionString = writeConnectionString;
        _readConnectionString = string.IsNullOrWhiteSpace(readConnectionString)
            ? null
            : readConnectionString.Trim();
    }

    public DbConnection Create()
    {
        return CreateWrite();
    }

    public DbConnection CreateRead()
    {
        return new NpgsqlConnection(_readConnectionString ?? _writeConnectionString);
    }

    public DbConnection CreateWrite()
    {
        return new NpgsqlConnection(_writeConnectionString);
    }
}
