using System.Text.Json;

namespace AppConfigEditor.Core;

public sealed class AppSettings
{
    public string? BasePath { get; set; }
}

/// <summary>
/// Persistenza delle impostazioni dell'app (per ora la sola base path) in
/// ~/.config/AppConfigEditor/settings.json (Linux) o %APPDATA% (Windows).
/// </summary>
public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public AppSettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFilePath();
    }

    public string FilePath => _filePath;

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Impostazioni corrotte: si riparte dai valori di default.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static string DefaultFilePath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(root))
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        return Path.Combine(root, "AppConfigEditor", "settings.json");
    }
}
