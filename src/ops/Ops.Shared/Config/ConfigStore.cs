using System.Text.Json;

namespace Ops.Shared.Config;

public sealed class ConfigStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public ConfigStore(string path)
    {
        _path = path;
    }

    public OpsConfig Load()
    {
        if (!File.Exists(_path))
        {
            var created = OpsConfig.CreateDefault();
            Save(created);
            return created;
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<OpsConfig>(json, Options) ?? OpsConfig.CreateDefault();
    }

    public void Save(OpsConfig config)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(_path, json);
    }
}
