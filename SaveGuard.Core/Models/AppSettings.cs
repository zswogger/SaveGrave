namespace SaveGuard.Core.Models;

/// <summary>
/// Application-wide, user-configurable preferences. Persisted separately from backup targets.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Launch Save Grave automatically when the user logs in.</summary>
    public bool LaunchAtStartup { get; set; }

    /// <summary>
    /// When true, closing the main window hides it to the system tray and protection keeps running;
    /// the app only exits via the tray's Exit action.
    /// </summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Set once the user has seen the "still running in the tray" notice.</summary>
    public bool CloseToTrayNoticeShown { get; set; }
}
