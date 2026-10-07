namespace SaveGrave.Core;

/// <summary>
/// Resolves the per-user application data locations Save Grave writes to, so configuration and
/// logs live under a stable cross-platform directory rather than beside the executable.
/// </summary>
public static class AppPaths
{
    // Per-user data folder name. Never shown in the UI.
    private const string AppFolderName = "SaveGrave";

    /// <summary>The root per-user directory, e.g. %APPDATA%\SaveGrave on Windows.</summary>
    public static string AppDataDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
                appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

            return Path.Combine(appData, AppFolderName);
        }
    }

    public static string ConfigFilePath => Path.Combine(AppDataDirectory, "targets.json");

    public static string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");

    public static string LogsDirectory => Path.Combine(AppDataDirectory, "logs");
}
