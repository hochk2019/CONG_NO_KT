using Ops.Agent.Services;

namespace Ops.Tests;

public sealed class IisBindingTests
{
    [Fact]
    public void ParseBindingInformation_SplitsComponents()
    {
        var (ip, port, host) = IisControl.ParseBindingInformation("*:8081:");

        Assert.Equal("*", ip);
        Assert.Equal(8081, port);
        Assert.Equal(string.Empty, host);
    }

    [Fact]
    public void BuildBindingInformation_BuildsString()
    {
        var binding = IisControl.BuildBindingInformation("*", 8081, string.Empty);

        Assert.Equal("*:8081:", binding);
    }
}
