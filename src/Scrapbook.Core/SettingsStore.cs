using System.Text.Json;

namespace Scrapbook.Core;

/// <summary>Reads and writes settings.json (plain JSON; contains nothing sensitive).</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _directory;

    public SettingsStore(string directory)
    {
        _directory = directory;
        SettingsPath = Path.Combine(directory, "settings.json");
    }

    public string SettingsPath { get; }

    /// <summary>Returns defaults when the file is missing or unreadable.</summary>
    public WindowSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new WindowSettings();
            return JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(SettingsPath), Json) ?? new WindowSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new WindowSettings();
        }
    }

    public void Save(WindowSettings settings)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Json));
    }
}
