namespace SaveGuard.Core;

/// <summary>
/// Resolves the per-user application data locations GameSaveGuard writes to, so configuration and
/// logs live under a stable cross-platform directory rather than beside the executable.
/// </summary>
public static class AppPaths
{
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

    public static string LogsDirectory => Path.Combine(AppDataDirectory, "logs");
}
