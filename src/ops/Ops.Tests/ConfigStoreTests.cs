using Ops.Shared.Config;

namespace Ops.Tests;

public class ConfigStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsConfig()
    {
        var temp = Path.GetTempFileName();
        try
        {
            var store = new ConfigStore(temp);
            var config = OpsConfig.CreateDefault() with
            {
                Agent = new AgentConfig { ApiKey = "test-key" }
            };

            store.Save(config);
            var loaded = store.Load();

            Assert.Equal("test-key", loaded.Agent.ApiKey);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
