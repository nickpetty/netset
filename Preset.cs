using System.Text.Json;

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
    public List<Preset> Presets { get; set; } = new();
}

/// <summary>Shareable preset files: {"version":1,"presets":[...]}. A bare array of presets is also accepted on import.</summary>
public static class PresetFile
{
    class Doc
    {
        public int Version { get; set; } = 1;
        public List<Preset> Presets { get; set; } = new();
    }

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void Export(string path, IEnumerable<Preset> presets) =>
        File.WriteAllText(path, JsonSerializer.Serialize(new Doc { Presets = presets.ToList() }, Json));

    /// <summary>Reads presets from a file. Throws InvalidDataException if it holds no usable presets.</summary>
    public static List<Preset> Import(string path)
    {
        List<Preset>? list;
        try
        {
            string text = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(text);
            list = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.Deserialize<List<Preset>>(Json)
                : doc.RootElement.Deserialize<Doc>(Json)?.Presets;
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
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { /* corrupt file: start fresh */ }

        var s = new Settings();
        s.Presets.Add(new Preset { Name = "Automatic (DHCP)", Dhcp = true, Favorite = true });
        return s;
    }

    public static void Save(Settings s)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(s, Json));
    }
}
