using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using SaveGuard.Core.Models;
using SaveGuard.Desktop.ViewModels;

namespace SaveGuard.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowChrome.Apply(this);
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
    }

    /// <summary>Shows a one-time notice that the app keeps protecting from the tray after close.</summary>
    public void ShowTrayNotice()
        => _ = ShowToastAsync("Save Grave is still protecting your saves from the system tray. Use the tray icon to open it or exit.");

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (ViewModel is not { } vm)
            return;

        vm.ShowAddGameDialogAsync = ShowAddGameDialogAsync;
        vm.ShowSettingsDialogAsync = ShowSettingsDialogAsync;
        vm.ConfirmRemoveAsync = ConfirmRemoveAsync;
        vm.ConfirmRestoreAsync = ConfirmRestoreAsync;
        vm.ConfirmDeleteAsync = ConfirmDeleteAsync;
        vm.CopyToClipboardAsync = CopyToClipboardAsync;
        vm.ShowMessageAsync = ShowToastAsync;
    }

    private Task<bool> ConfirmDeleteAsync(Snapshot snapshot)
        => ShowConfirmAsync(
            "Delete this recovery point?",
            "This permanently removes the selected snapshot from disk. Your current save is not affected.",
            confirmText: "Delete",
            danger: true);

    private async Task CopyToClipboardAsync(string text)
    {
        var clipboard = Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(text);
            await ShowToastAsync("Copied to clipboard.");
        }
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        if (ViewModel is { } vm)
            await vm.InitializeAsync();
    }

    // ===================== Add Game =====================

    private Task<BackupTarget?> ShowAddGameDialogAsync()
    {
        var dialog = new AddGameView();
        var scrim = ShowModal(dialog);
        var tcs = new TaskCompletionSource<BackupTarget?>();

        _ = dialog.Completion.ContinueWith(t =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                CloseModal(scrim);
                tcs.TrySetResult(t.Result);
            });
        }, TaskScheduler.Default);

        return tcs.Task;
    }

    // ===================== Settings =====================

    private Task ShowSettingsDialogAsync()
    {
        if (ViewModel is not { } vm)
            return Task.CompletedTask;

        var dialog = new SettingsView(new SettingsViewModel(vm));
        var scrim = ShowModal(dialog);
        var tcs = new TaskCompletionSource();

        _ = dialog.Completion.ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                CloseModal(scrim);
                tcs.TrySetResult();
            });
        }, TaskScheduler.Default);

        return tcs.Task;
    }

    // ===================== Confirmations =====================

    private Task<bool> ConfirmRemoveAsync(GameItemViewModel item)
        => ShowConfirmAsync(
            "Remove from Save Grave?",
            $"\"{item.DisplayName}\" will no longer be protected. Your saves and existing backups are not deleted.",
            confirmText: "Remove",
            danger: true);

    private Task<bool> ConfirmRestoreAsync(Snapshot snapshot)
        => ShowConfirmAsync(
            "Restore this backup?",
            "Your current save will be preserved as a safety snapshot before this recovery point is restored.",
            confirmText: "Restore Backup",
            danger: false);

    private Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool danger)
    {
        var tcs = new TaskCompletionSource<bool>();

        var cancel = new Button { Content = "Cancel" };
        var confirm = new Button { Content = confirmText };
        confirm.Classes.Add(danger ? "danger" : "primary");

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { cancel, confirm },
        };

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = title, Classes = { "h2" } },
                new TextBlock { Text = message, Classes = { "secondary" }, TextWrapping = TextWrapping.Wrap },
                buttons,
            },
        };

        var dialog = new Border
        {
            Classes = { "dialogSurface" },
            Padding = new Thickness(26, 24),
            Width = 440,
            Child = content,
        };

        var scrim = ShowModal(dialog);

        void Finish(bool result)
        {
            CloseModal(scrim);
            tcs.TrySetResult(result);
        }

        cancel.Click += (_, _) => Finish(false);
        confirm.Click += (_, _) => Finish(true);

        return tcs.Task;
    }

    // ===================== Toast =====================

    private Task ShowToastAsync(string message)
    {
        var toast = new Border
        {
            Background = Brush("SurfaceBrush"),
            BorderBrush = Brush("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(18, 12),
            Margin = new Thickness(0, 0, 0, 28),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            MaxWidth = 520,
            Child = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush("PrimaryTextBrush"),
            },
        };

        OverlayHost.Children.Add(toast);
        OverlayHost.IsHitTestVisible = true;

        _ = Task.Delay(3200).ContinueWith(_ =>
            Dispatcher.UIThread.Post(() =>
            {
                OverlayHost.Children.Remove(toast);
                if (OverlayHost.Children.Count == 0)
                    OverlayHost.IsHitTestVisible = false;
            }), TaskScheduler.Default);

        return Task.CompletedTask;
    }

    // ===================== Overlay plumbing =====================

    /// <summary>Adds a dimmed scrim hosting the given dialog content, centered. Returns the scrim.</summary>
    private Border ShowModal(Control dialogContent)
    {
        var scrim = new Border
        {
            Background = Brush("OverlayScrimBrush"),
            Child = new Panel { Children = { Center(dialogContent) } },
        };

        scrim.Opacity = 0;
        scrim.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = System.TimeSpan.FromMilliseconds(140) },
        };

        OverlayHost.Children.Add(scrim);
        OverlayHost.IsHitTestVisible = true;
        Dispatcher.UIThread.Post(() => scrim.Opacity = 1, DispatcherPriority.Render);
        return scrim;
    }

    private void CloseModal(Border scrim)
    {
        OverlayHost.Children.Remove(scrim);
        if (OverlayHost.Children.Count == 0)
            OverlayHost.IsHitTestVisible = false;
    }

    private static Control Center(Control content)
    {
        content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        return content;
    }

    private static IBrush? Brush(string key)
        => Application.Current!.TryGetResource(key, ThemeVariant.Dark, out var value) ? value as IBrush : null;
}
