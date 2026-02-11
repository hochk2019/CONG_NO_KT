using Ops.Agent.Services;
using Ops.Shared.Config;
using System.Runtime.Versioning;

namespace Ops.Tests;

[SupportedOSPlatform("windows")]
public class SqlConsoleServiceTests
{
    [Fact]
    public void BuildArgs_IncludesCoreFlagsAndConnection()
    {
        var info = new DbConnectionInfo("db.local", 5432, "congno", "app", "secret");
        var args = SqlConsoleService.BuildArgs("select 1", info);

        Assert.Contains("-X", args);
        Assert.Contains("-v ON_ERROR_STOP=1", args);
        Assert.Contains("-P pager=off", args);
        Assert.Contains("-h", args);
        Assert.Contains("db.local", args);
        Assert.Contains("-p 5432", args);
        Assert.Contains("-U", args);
        Assert.Contains("app", args);
        Assert.Contains("-d", args);
        Assert.Contains("congno", args);
        Assert.Contains("-c", args);
    }

    [Fact]
    public void BuildPreviewSql_WrapsSelectWithLimit()
    {
        var sql = "select * from customers";
        var preview = SqlConsoleService.BuildPreviewSql(sql);

        Assert.Contains("from (select * from customers)", preview, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("limit 50", preview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPreviewSql_WrapsNonSelectInTransaction()
    {
        var sql = "update customers set name = 'a'";
        var preview = SqlConsoleService.BuildPreviewSql(sql);

        Assert.Contains("BEGIN", preview, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROLLBACK", preview, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("UPDATE 3", 3)]
    [InlineData("INSERT 0 1", 1)]
    [InlineData("DELETE 0", 0)]
    [InlineData("SELECT 12", 12)]
    public void TryParseRowsAffected_ParsesRowCount(string stdout, int expected)
    {
        var result = SqlConsoleService.TryParseRowsAffected(stdout);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryParseRowsAffected_ReturnsNullWhenNoMatch()
    {
        var result = SqlConsoleService.TryParseRowsAffected("ERROR: syntax error");
        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_PreviewUsesTransactionAndParsesRows()
    {
        var runner = new FakeSqlRunner(new CommandResult(0, "UPDATE 2", string.Empty));
        var service = new SqlConsoleService(runner);
        var config = new OpsConfig
        {
            Database = new DatabaseConfig
            {
                ConnectionString = "Host=localhost;Port=5432;Database=congno;Username=app;Password=secret",
                PgBinPath = "C:\\pg"
            }
        };

        var response = await service.ExecuteAsync(config, "update customers set name = 'a'", true, CancellationToken.None);

        Assert.NotNull(runner.Args);
        Assert.Contains("BEGIN;", runner.Args, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, response.RowsAffected);
    }

    private sealed class FakeSqlRunner(CommandResult result) : ISqlCommandRunner
    {
        public string? ExePath { get; private set; }
        public string? Args { get; private set; }
        public string? ConnectionString { get; private set; }

        public Task<CommandResult> RunAsync(string exePath, string args, string connectionString, CancellationToken ct)
        {
            ExePath = exePath;
            Args = args;
            ConnectionString = connectionString;
            return Task.FromResult(result);
        }
    }
}
