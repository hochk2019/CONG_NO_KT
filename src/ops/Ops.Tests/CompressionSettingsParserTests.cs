using Ops.Shared.Models;

namespace Ops.Tests;

public class CompressionSettingsParserTests
{
    [Fact]
    public void Parse_NullValues_ReturnsFalse()
    {
        var json = "{\"static\":null,\"dynamic\":null}";

        var result = CompressionSettingsParser.Parse(json);

        Assert.False(result.StaticEnabled);
        Assert.False(result.DynamicEnabled);
    }

    [Fact]
    public void Parse_BooleanValues_ReturnsExpected()
    {
        var json = "{\"static\":true,\"dynamic\":false}";

        var result = CompressionSettingsParser.Parse(json);

        Assert.True(result.StaticEnabled);
        Assert.False(result.DynamicEnabled);
    }
}
