namespace Ops.Shared.Config;

public static class IisConfigUpdater
{
    public static OpsConfig ApplyIisConfig(OpsConfig config, string? siteName, string? appPoolName)
    {
        var cleanedSite = string.IsNullOrWhiteSpace(siteName) ? config.Frontend.IisSiteName : siteName.Trim();
        var cleanedPool = string.IsNullOrWhiteSpace(appPoolName) ? config.Frontend.AppPoolName : appPoolName.Trim();

        return config with
        {
            Frontend = config.Frontend with
            {
                IisSiteName = cleanedSite,
                AppPoolName = cleanedPool
            }
        };
    }
}
