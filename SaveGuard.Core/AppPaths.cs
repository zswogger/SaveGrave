namespace SaveGuard.Core;

/// <summary>
/// Resolves the per-user application data locations Save Grave writes to, so configuration and
/// logs live under a stable cross-platform directory rather than beside the executable.
/// </summary>
public static class AppPaths
{
    // The on-disk data folder name is intentionally kept as the original app id so an existing
    // user's configuration, logs, and settings survive the rebrand to "Save Grave" without a
    // migration step. This name is never shown in the UI.
    private const string AppFolderName = "GameSaveGuard";

    /// <summary>The root per-user directory, e.g. %APPDATA%\GameSaveGuard on Windows.</summary>
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
