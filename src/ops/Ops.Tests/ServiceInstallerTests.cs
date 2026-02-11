using Ops.Agent.Services;
using Ops.Shared.Config;
using System.Runtime.Versioning;

namespace Ops.Tests;

[SupportedOSPlatform("windows")]
public class ServiceInstallerTests
{
    [Fact]
    public void ResolveBackendExePath_UsesOverride()
    {
        var config = OpsConfig.CreateDefault();
        var path = ServiceInstaller.ResolveBackendExePath(config, "D:\\apps\\api\\app.exe");
        Assert.Equal("D:\\apps\\api\\app.exe", path);
    }

    [Fact]
    public void ResolveBackendExePath_UsesConfigWhenNull()
    {
        var config = OpsConfig.CreateDefault() with
        {
            Backend = new BackendConfig { AppPath = "C:\\apps\\congno\\api", ExeName = "CongNoGolden.Api.exe" }
        };

        var path = ServiceInstaller.ResolveBackendExePath(config, null);
        Assert.Equal("C:\\apps\\congno\\api\\CongNoGolden.Api.exe", path);
    }
}
