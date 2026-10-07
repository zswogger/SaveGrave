namespace SaveGrave.Core.Services;

/// <summary>
/// Registers or unregisters the application to launch automatically at user login. Implementations
/// are platform-specific; unsupported platforms report <see cref="IsSupported"/> = false.
/// </summary>
public interface IStartupManager
{
    /// <summary>True when launch-at-startup can be configured on the current platform.</summary>
    bool IsSupported { get; }

    /// <summary>Returns whether the app is currently registered to launch at startup.</summary>
    bool IsEnabled();

    /// <summary>Enables or disables launch-at-startup. No-op when unsupported.</summary>
    void SetEnabled(bool enabled);
}
