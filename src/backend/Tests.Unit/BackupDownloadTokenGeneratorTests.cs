using System.Reflection;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupDownloadTokenGeneratorTests
{
    [Fact]
    public void CreateToken_ReturnsTokenAndExpiry()
    {
        var type = Type.GetType("CongNoGolden.Application.Backups.BackupDownloadTokenGenerator, CongNoGolden.Application");
        Assert.NotNull(type);

        var method = type!.GetMethod("CreateToken", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        var now = new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero);
        var ttl = TimeSpan.FromMinutes(30);

        var result = method!.Invoke(null, new object[] { now, ttl });
        Assert.NotNull(result);

        var tokenProp = result!.GetType().GetProperty("Token");
        var expiresProp = result!.GetType().GetProperty("ExpiresAt");
        Assert.NotNull(tokenProp);
        Assert.NotNull(expiresProp);

        var token = tokenProp!.GetValue(result) as string;
        var expires = (DateTimeOffset)expiresProp!.GetValue(result)!;

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(now.AddMinutes(30), expires);
    }
}
