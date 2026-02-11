using System.Text.Json;
using System.Text.RegularExpressions;

namespace CongNoGolden.Infrastructure.Services;

public static class ZaloWebhookParser
{
    private static readonly Regex LinkCodeRegex = new(
        @"(?i)\b(?:link|lk|cn)\s*([A-Z0-9]{6,12})\b",
        RegexOptions.Compiled);

    public static bool TryExtractUserId(JsonElement root, out string? userId)
    {
        return TryGetString(root, out userId, "sender", "id")
            || TryGetString(root, out userId, "sender_id")
            || TryGetString(root, out userId, "user_id");
    }

    public static bool TryExtractRecipientOaId(JsonElement root, out string? oaId)
    {
        return TryGetString(root, out oaId, "recipient", "id")
            || TryGetString(root, out oaId, "oa_id")
            || TryGetString(root, out oaId, "recipient_id");
    }

    public static bool TryExtractMessageText(JsonElement root, out string? message)
    {
        return TryGetString(root, out message, "message", "text")
            || TryGetString(root, out message, "message", "content")
            || TryGetString(root, out message, "text")
            || TryGetString(root, out message, "content");
    }

    public static bool TryExtractLinkCode(string? message, out string? code)
    {
        code = null;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var match = LinkCodeRegex.Match(message.Trim());
        if (!match.Success)
        {
            return false;
        }

        code = match.Groups[1].Value.ToUpperInvariant();
        return true;
    }

    private static bool TryGetString(JsonElement element, out string? value, params string[] path)
    {
        value = null;
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return false;
            }
        }

        if (current.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = current.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }
}
