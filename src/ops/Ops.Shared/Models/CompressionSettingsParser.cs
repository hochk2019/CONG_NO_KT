using System.Text.Json;

namespace Ops.Shared.Models;

public static class CompressionSettingsParser
{
    public static CompressionSettingsDto Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new CompressionSettingsDto(false, false);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var staticEnabled = ReadBoolean(root, "static");
            var dynamicEnabled = ReadBoolean(root, "dynamic");
            return new CompressionSettingsDto(staticEnabled, dynamicEnabled);
        }
        catch (JsonException)
        {
            return new CompressionSettingsDto(false, false);
        }
    }

    private static bool ReadBoolean(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element))
            return false;

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(element.GetString(), out var parsed) && parsed,
            JsonValueKind.Number => element.TryGetInt32(out var number) && number != 0,
            _ => false
        };
    }
}
