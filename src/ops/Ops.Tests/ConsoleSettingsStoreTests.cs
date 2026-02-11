using Ops.Shared.Console;

namespace Ops.Tests;

public class ConsoleSettingsStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsSettings()
    {
        var store = new ConsoleSettingsStore();
        var settings = new ConsoleSettings
        {
            Profiles =
            [
                new ConsoleProfile
                {
                    Name = "Server A",
                    BaseUrl = "http://127.0.0.1:6090",
                    ApiKey = "abc"
                }
            ]
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal("abc", loaded.Profiles[0].ApiKey);
    }
}
