using CommunityToolkit.Mvvm.ComponentModel;

namespace SaveGuard.Desktop.ViewModels;

/// <summary>Backs the Settings dialog. Applies and persists changes immediately via the host.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private bool _initializing;

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        _initializing = true;
        LaunchAtStartup = main.Settings.LaunchAtStartup;
        CloseToTray = main.Settings.CloseToTray;
        _initializing = false;
    }

    public bool StartupSupported => _main.StartupSupported;

    public string StartupHint => StartupSupported
        ? "Save Grave will start automatically when you log in, so protection does not depend on launching it manually."
        : "Launch at startup is not available on this platform.";

    [ObservableProperty]
    public partial bool LaunchAtStartup { get; set; }

    [ObservableProperty]
    public partial bool CloseToTray { get; set; }

    partial void OnLaunchAtStartupChanged(bool value)
    {
        if (!_initializing)
            _main.SetLaunchAtStartup(value);
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        if (!_initializing)
            _main.SetCloseToTray(value);
    }
}
