using Ops.Agent.Services;
using System.Runtime.Versioning;

namespace Ops.Tests;

[SupportedOSPlatform("windows")]
public class BackupRunnerTests
{
    [Fact]
    public void BuildRestoreArgs_IncludesNoOwner()
    {
        var info = BackupRunner.ParseConnectionInfo("Host=localhost;Port=5432;Database=congno_golden;Username=app;Password=secret");
        var args = BackupRunner.BuildRestoreArgs("file.dump", info);
        Assert.Contains("--no-owner", args);
        Assert.Contains("--no-privileges", args);
    }

    [Fact]
    public void ParseConnectionInfo_ParsesCoreFields()
    {
        var info = BackupRunner.ParseConnectionInfo("Host=127.0.0.1;Port=5433;Database=congno;Username=app;Password=secret");
        Assert.Equal("127.0.0.1", info.Host);
        Assert.Equal(5433, info.Port);
        Assert.Equal("congno", info.Database);
        Assert.Equal("app", info.Username);
        Assert.Equal("secret", info.Password);
    }

    [Fact]
    public void BuildDumpArgs_IncludesConnectionOptions()
    {
        var info = new DbConnectionInfo("db.local", 5432, "congno", "app", "secret");
        var args = BackupRunner.BuildDumpArgs("file.dump", info);
        Assert.Contains("-h", args);
        Assert.Contains("db.local", args);
        Assert.Contains("-p 5432", args);
        Assert.Contains("-U", args);
        Assert.Contains("app", args);
    }
}
