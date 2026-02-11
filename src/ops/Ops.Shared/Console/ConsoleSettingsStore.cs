using System.IO;
using System.Text.Json;

namespace Ops.Shared.Console;

public sealed class ConsoleSettingsStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path;

    public ConsoleSettingsStore()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CongNoOps");
        _path = Path.Combine(root, "console-settings.json");
    }

    public ConsoleSettings Load()
    {
        if (!File.Exists(_path))
        {
            var created = ConsoleSettings.Default;
            Save(created);
            return created;
        }

        var json = File.ReadAllText(_path);
        var settings = JsonSerializer.Deserialize<ConsoleSettings>(json, Options) ?? ConsoleSettings.Default;
        return Normalize(settings);
    }

    public void Save(ConsoleSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
        var json = JsonSerializer.Serialize(Normalize(settings), Options);
        File.WriteAllText(_path, json);
    }

    private static ConsoleSettings Normalize(ConsoleSettings settings)
    {
        var profiles = settings.Profiles ?? new List<ConsoleProfile>();
        if (profiles.Count == 0)
        {
            var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
                ? "http://localhost:6090"
                : settings.BaseUrl;
            var legacyProfile = new ConsoleProfile
            {
                Name = "Server 1",
                BaseUrl = baseUrl,
                ApiKey = settings.ApiKey ?? string.Empty
            };
            profiles = new List<ConsoleProfile> { legacyProfile };
            settings = settings with { Profiles = profiles, ActiveProfileId = legacyProfile.Id };
        }

        var activeId = settings.ActiveProfileId;
        if (string.IsNullOrWhiteSpace(activeId) || profiles.All(p => p.Id != activeId))
            activeId = profiles[0].Id;

        if (settings.AutoRefreshSeconds <= 0)
            settings = settings with { AutoRefreshSeconds = 10 };

        return settings with { ActiveProfileId = activeId, Profiles = profiles };
    }
}
