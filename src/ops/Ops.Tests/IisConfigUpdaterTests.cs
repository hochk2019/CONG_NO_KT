using Ops.Shared.Config;

namespace Ops.Tests;

public class IisConfigUpdaterTests
{
    [Fact]
    public void ApplyIisConfig_UpdatesSiteAndPool()
    {
        var config = new OpsConfig
        {
            Frontend = new FrontendConfig
            {
                IisSiteName = "OldSite",
                AppPoolName = "OldPool"
            }
        };

        var updated = IisConfigUpdater.ApplyIisConfig(config, "NewSite", "NewPool");

        Assert.Equal("NewSite", updated.Frontend.IisSiteName);
        Assert.Equal("NewPool", updated.Frontend.AppPoolName);
    }

    [Fact]
    public void ApplyIisConfig_EmptyInputsKeepExisting()
    {
        var config = new OpsConfig
        {
            Frontend = new FrontendConfig
            {
                IisSiteName = "ExistingSite",
                AppPoolName = "ExistingPool"
            }
        };

        var updated = IisConfigUpdater.ApplyIisConfig(config, "   ", "");

        Assert.Equal("ExistingSite", updated.Frontend.IisSiteName);
        Assert.Equal("ExistingPool", updated.Frontend.AppPoolName);
    }
}
