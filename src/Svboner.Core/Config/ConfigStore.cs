using System.Text.Json;
using System.Text.Json.Serialization;
using Svboner.Core.Models;

namespace Svboner.Core.Config;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _configPath;
    private readonly object _saveLock = new();
    private SvbonerConfig _current;

    public ConfigStore(string? configPath = null)
    {
        var dir = configPath is not null
            ? Path.GetDirectoryName(configPath)!
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SVBONER");

        Directory.CreateDirectory(dir);
        _configPath = configPath ?? Path.Combine(dir, "config.json");
        _current = Load();
    }

    public string ConfigPath => _configPath;

    /// <summary>Returns the current in-memory config snapshot.</summary>
    public SvbonerConfig Get() => _current;

    /// <summary>Mutates the config in-place and persists it to disk.</summary>
    public void Update(Action<SvbonerConfig> mutator)
    {
        lock (_saveLock)
        {
            mutator(_current);
            Persist(_current);
        }
    }

    /// <summary>Replaces the entire config and persists it.</summary>
    public void Replace(SvbonerConfig config)
    {
        lock (_saveLock)
        {
            _current = config;
            Persist(_current);
        }
    }

    private SvbonerConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            var defaults = new SvbonerConfig();
            Persist(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<SvbonerConfig>(json, JsonOptions) ?? new SvbonerConfig();
        }
        catch
        {
            return new SvbonerConfig();
        }
    }

    private void Persist(SvbonerConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configPath, json);
    }
}
