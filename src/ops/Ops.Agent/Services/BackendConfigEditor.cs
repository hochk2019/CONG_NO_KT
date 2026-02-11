using System.Text.Json;
using System.Text.Json.Nodes;
using Ops.Shared.Config;
using Ops.Shared.Models;

namespace Ops.Agent.Services;

public sealed class BackendConfigEditor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string ResolveAppSettingsPath(OpsConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.Backend.AppSettingsPath))
            return config.Backend.AppSettingsPath;

        return Path.Combine(config.Backend.AppPath, "appsettings.json");
    }

    public BackendLogLevelDto GetLogLevel(OpsConfig config)
    {
        var node = LoadConfigNode(config);
        var defaultLevel = node?["Logging"]?["LogLevel"]?["Default"]?.ToString()
                           ?? node?["Serilog"]?["MinimumLevel"]?["Default"]?.ToString()
                           ?? "Information";
        var serilogLevel = node?["Serilog"]?["MinimumLevel"]?["Default"]?.ToString();
        return new BackendLogLevelDto(defaultLevel, serilogLevel);
    }

    public BackendJobSettingsDto GetJobSettings(OpsConfig config)
    {
        var node = LoadConfigNode(config);
        var remindersEnabled = node?["Reminders"]?["AutoRunEnabled"]?.GetValue<bool?>() ?? false;
        var reconcileEnabled = node?["InvoiceReconcile"]?["AutoRunEnabled"]?.GetValue<bool?>() ?? false;
        return new BackendJobSettingsDto(remindersEnabled, reconcileEnabled);
    }

    public BackendLogLevelDto UpdateLogLevel(OpsConfig config, string level)
    {
        var node = LoadConfigNode(config) ?? new JsonObject();
        SetNodeString(node, "Logging:LogLevel:Default", level);
        SetNodeString(node, "Serilog:MinimumLevel:Default", level);
        SaveConfigNode(config, node);
        return GetLogLevel(config);
    }

    public BackendJobSettingsDto UpdateJobSettings(OpsConfig config, BackendJobSettingsUpdateRequest request)
    {
        var node = LoadConfigNode(config) ?? new JsonObject();
        SetNodeBoolean(node, "Reminders:AutoRunEnabled", request.RemindersEnabled);
        SetNodeBoolean(node, "InvoiceReconcile:AutoRunEnabled", request.InvoiceReconcileEnabled);
        SaveConfigNode(config, node);
        return GetJobSettings(config);
    }

    private JsonNode? LoadConfigNode(OpsConfig config)
    {
        var path = ResolveAppSettingsPath(config);
        if (!File.Exists(path))
            return null;

        return JsonNode.Parse(File.ReadAllText(path));
    }

    private void SaveConfigNode(OpsConfig config, JsonNode node)
    {
        var path = ResolveAppSettingsPath(config);
        var json = node.ToJsonString(JsonOptions);
        File.WriteAllText(path, json);
    }

    private static void SetNodeString(JsonNode root, string path, string value)
    {
        var node = EnsurePath(root, path);
        node.ReplaceWith(JsonValue.Create(value));
    }

    private static void SetNodeBoolean(JsonNode root, string path, bool value)
    {
        var node = EnsurePath(root, path);
        node.ReplaceWith(JsonValue.Create(value));
    }

    private static JsonNode EnsurePath(JsonNode root, string path)
    {
        var parts = path.Split(':', StringSplitOptions.RemoveEmptyEntries);
        JsonNode current = root;
        foreach (var part in parts)
        {
            if (current is not JsonObject obj)
                throw new InvalidOperationException("Config root must be an object");

            if (obj[part] is null)
                obj[part] = new JsonObject();

            current = obj[part]!;
        }

        return current;
    }
}
