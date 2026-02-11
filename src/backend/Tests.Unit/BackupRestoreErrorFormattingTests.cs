using System.Reflection;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupRestoreErrorFormattingTests
{
    [Fact]
    public void BuildRestoreFailureMessage_IncludesExitCodeAndStderr()
    {
        var type = typeof(BackupService);
        var method = type.GetMethod(
            "BuildRestoreFailureMessage",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = new BackupProcessResult(1, "stdout", "restore failed");
        var message = method!.Invoke(null, new object[] { result }) as string;

        Assert.NotNull(message);
        Assert.Contains("1", message!);
        Assert.Contains("restore failed", message!);
    }

    [Fact]
    public void BuildRestoreFailureMessage_HintsWhenOwnerMissing()
    {
        var type = typeof(BackupService);
        var method = type.GetMethod(
            "BuildRestoreFailureMessage",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = new BackupProcessResult(1, string.Empty, "ERROR: must be owner of table users");
        var message = method!.Invoke(null, new object[] { result }) as string;

        Assert.NotNull(message);
        Assert.Contains("ConnectionStrings__Migrations", message!);
    }
}
