using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetSet;

public class Preset
{
    public string Name { get; set; } = "";
    public bool Dhcp { get; set; } = true;
    public string Ip { get; set; } = "";
    public string Mask { get; set; } = "255.255.255.0";
    public string Gateway { get; set; } = "";
    public string Dns1 { get; set; } = "";
    public string Dns2 { get; set; } = "";
    public bool Favorite { get; set; }

    public Preset Clone() => (Preset)MemberwiseClone();
}

public class Settings
{
    public string? Adapter { get; set; }
    public bool AutoCheckUpdates { get; set; } = true;
    public string? NotifiedUpdate { get; set; }
    public List<Preset> Presets { get; set; } = new();
}

public class PresetDoc
{
    public int Version { get; set; } = 1;
    public List<Preset> Presets { get; set; } = new();
}

// Source-generated serializers: no runtime reflection, so the exe can be trimmed safely.
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
partial class SettingsJson : JsonSerializerContext { }

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PresetDoc))]
[JsonSerializable(typeof(List<Preset>))]
partial class PresetFileJson : JsonSerializerContext { }

/// <summary>Shareable preset files: {"version":1,"presets":[...]}. A bare array of presets is also accepted on import.</summary>
public static class PresetFile
{
    public static void Export(string path, IEnumerable<Preset> presets) =>
        File.WriteAllText(path, JsonSerializer.Serialize(new PresetDoc { Presets = presets.ToList() }, PresetFileJson.Default.PresetDoc));

    /// <summary>Reads presets from a file. Throws InvalidDataException if it holds no usable presets.</summary>
    public static List<Preset> Import(string path)
    {
        List<Preset>? list;
        try
        {
            string text = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(text);
            list = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.Deserialize(PresetFileJson.Default.ListPreset)
                : doc.RootElement.Deserialize(PresetFileJson.Default.PresetDoc)?.Presets;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("This file is not a valid NetSet preset file.", ex);
        }

        var valid = (list ?? new()).Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name)).ToList();
        if (valid.Count == 0) throw new InvalidDataException("No presets were found in this file.");
        foreach (var p in valid) p.Name = p.Name.Trim();
        return valid;
    }
}

public static class SettingsStore
{
    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetSet");
    static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static bool Exists => File.Exists(FilePath);

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), SettingsJson.Default.Settings) ?? new Settings();
        }
        catch { /* corrupt file: start fresh */ }

        var s = new Settings();
        s.Presets.Add(new Preset { Name = "Automatic (DHCP)", Dhcp = true, Favorite = true });
        return s;
    }

    public static void Save(Settings s)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(s, SettingsJson.Default.Settings));
    }
}
