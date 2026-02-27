using System.Reflection;
using CongNoGolden.Infrastructure.Services;
using Npgsql;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupRestoreCommandArgumentsTests
{
    [Fact]
    public void BuildPgRestoreArguments_UsesPortableRestoreFlags()
    {
        var method = typeof(BackupService).GetMethod(
            "BuildPgRestoreArguments",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Username = "congno_app",
            Database = "congno_golden"
        };

        var arguments = method!.Invoke(null, new object[] { builder, "/tmp/congno.dump" }) as string;

        Assert.NotNull(arguments);
        Assert.Contains("--clean", arguments!);
        Assert.Contains("--if-exists", arguments);
        Assert.Contains("--no-owner", arguments);
        Assert.Contains("--no-privileges", arguments);
        Assert.Contains("--exit-on-error", arguments);
        Assert.Contains("\"/tmp/congno.dump\"", arguments);
    }

    [Fact]
    public void BuildPgDumpArguments_DisablesOwnerAndPrivilegeMetadata()
    {
        var method = typeof(BackupService).GetMethod(
            "BuildPgDumpArguments",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Username = "congno_app",
            Database = "congno_golden"
        };

        var arguments = method!.Invoke(null, new object[] { builder, "/tmp/congno.dump" }) as string;

        Assert.NotNull(arguments);
        Assert.Contains(" -O ", $" {arguments} ");
        Assert.Contains(" -x ", $" {arguments} ");
        Assert.Contains("-F c", arguments);
        Assert.Contains("\"/tmp/congno.dump\"", arguments);
    }
}
