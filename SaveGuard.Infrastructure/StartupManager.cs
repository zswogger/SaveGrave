using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using SaveGuard.Core.Services;

namespace SaveGuard.Infrastructure;

/// <summary>
/// Launch-at-login registration. Windows is implemented via the per-user <c>Run</c> registry key.
/// macOS and Linux are not implemented yet and report as unsupported (no-op) so the UI can hide or
/// disable the option.
/// </summary>
public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // Kept as the original app id so an existing startup registration is still recognized after the
    // rebrand (prevents a duplicate/orphaned Run entry). Not shown in the UI.
    private const string ValueName = "GameSaveGuard";

    public static IStartupManager Create()
        => OperatingSystem.IsWindows() ? new WindowsStartupManager() : new UnsupportedStartupManager();

    private sealed class UnsupportedStartupManager : IStartupManager
    {
        public bool IsSupported => false;
        public bool IsEnabled() => false;
        public void SetEnabled(bool enabled) { /* no-op on unsupported platforms */ }
    }

    [SupportedOSPlatform("windows")]
    private sealed class WindowsStartupManager : IStartupManager
    {
        public bool IsSupported => true;

        public bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is not null;
        }

        public void SetEnabled(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
                return;

            if (enabled)
            {
                var exe = GetExecutablePath();
                if (exe is not null)
                    key.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }

        private static string? GetExecutablePath()
        {
            // Prefer the actual host process executable (the apphost .exe), not the managed dll.
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
                return path;

            return Process.GetCurrentProcess().MainModule?.FileName;
        }
    }
}
