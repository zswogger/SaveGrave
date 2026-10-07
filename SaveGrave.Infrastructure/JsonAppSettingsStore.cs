using System.Text.Json;
using SaveGrave.Core;
using SaveGrave.Core.Models;
using SaveGrave.Core.Services;

namespace SaveGrave.Infrastructure;

/// <summary>
/// Persists <see cref="AppSettings"/> as a JSON file under the per-user application data directory,
/// alongside the backup-target configuration.
/// </summary>
public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _path;

    public JsonAppSettingsStore(string? path = null)
    {
        _path = path ?? AppPaths.SettingsFilePath;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new AppSettings();

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch
        {
            // Corrupt or unreadable settings should never prevent the app from starting.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, SerializerOptions));
        File.Move(tempPath, _path, overwrite: true);
    }
}
