using System.Text.Json;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class ZaloWebhookParserTests
{
    [Fact]
    public void TryExtractLinkCode_Reads_Code_From_Text()
    {
        var text = "LINK 123456";
        var ok = ZaloWebhookParser.TryExtractLinkCode(text, out var code);
        Assert.True(ok);
        Assert.Equal("123456", code);
    }

    [Fact]
    public void TryExtractUserId_Reads_Sender_Id()
    {
        var json = """
        {
          "sender": { "id": "user-123" },
          "message": { "text": "LINK 654321" }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var ok = ZaloWebhookParser.TryExtractUserId(doc.RootElement, out var userId);
        Assert.True(ok);
        Assert.Equal("user-123", userId);
    }

    [Fact]
    public void TryExtractMessageText_Reads_Message_Text()
    {
        var json = """
        {
          "user_id": "user-456",
          "message": { "content": "LINK ABC123" }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var ok = ZaloWebhookParser.TryExtractMessageText(doc.RootElement, out var message);
        Assert.True(ok);
        Assert.Equal("LINK ABC123", message);
    }
}
