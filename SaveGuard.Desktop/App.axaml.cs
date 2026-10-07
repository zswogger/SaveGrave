using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using SaveGuard.Desktop.ViewModels;
using SaveGuard.Desktop.Views;

namespace SaveGuard.Desktop;

public partial class App : Application
{
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The app can keep running in the tray after the window is closed, so it must not quit
            // when the main window closes; it exits explicitly via the tray's Exit action.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _viewModel = new MainViewModel();
            _mainWindow = new MainWindow { DataContext = _viewModel };
            _viewModel.ActivateWindow = ShowMainWindow;

            _mainWindow.Closing += OnMainWindowClosing;
            desktop.MainWindow = _mainWindow;

            SetupTrayIcon(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // When "close to tray" is enabled, intercept the window-close and hide instead of exiting so
        // protection keeps running in the background.
        if (_isExiting)
            return;

        if (_viewModel is { Settings.CloseToTray: true } vm)
        {
            e.Cancel = true;
            _mainWindow?.Hide();

            if (!vm.Settings.CloseToTrayNoticeShown)
            {
                vm.Settings.CloseToTrayNoticeShown = true;
                vm.SaveSettings();
                ShowTrayNotice();
            }
        }
        else if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Close-to-tray disabled: closing the window exits the app.
            ExitApplication(desktop);
        }
    }

    private bool _isExiting;

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();

        var open = new NativeMenuItem("Open Save Grave");
        open.Click += (_, _) => ShowMainWindow();

        var snapshot = new NativeMenuItem("Take Snapshot (all games)");
        snapshot.Click += (_, _) => _viewModel?.SnapshotAllCommand.Execute(null);

        var pause = new NativeMenuItem("Pause All Protection");
        pause.Click += (_, _) => _viewModel?.PauseAllCommand.Execute(null);

        var resume = new NativeMenuItem("Resume All Protection");
        resume.Click += (_, _) => _viewModel?.ResumeAllCommand.Execute(null);

        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => ExitApplication(desktop);

        menu.Add(open);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(snapshot);
        menu.Add(pause);
        menu.Add(resume);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exit);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Save Grave — protection active",
            Menu = menu,
            IsVisible = true,
        };

        try
        {
            _trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://SaveGrave/Assets/save-grave-logo.ico")));
        }
        catch
        {
            // Tray icon image is non-essential; the menu still works without it.
        }

        _trayIcon.Clicked += (_, _) => ShowMainWindow();

        // The tray icon must be registered on the Application via the TrayIcon.Icons attached
        // property, otherwise it is never shown by the OS.
        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
            return;

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ShowTrayNotice()
    {
        _mainWindow?.ShowTrayNotice();
    }

    private void ExitApplication(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _isExiting = true;
        _viewModel?.Shutdown();
        _trayIcon?.Dispose();
        desktop.Shutdown();
    }
}
