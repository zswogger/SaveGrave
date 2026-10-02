using Avalonia.Controls;
using Avalonia.Layout;

namespace SaveGuard.Desktop.Views;

/// <summary>Minimal yes/no confirmation dialog, built in code to avoid extra XAML.</summary>
internal static class ConfirmDialog
{
    public static Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var cancelButton = new Button { Content = "Cancel" };
        var confirmButton = new Button { Content = "Confirm", IsDefault = true };

        var dialog = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelButton, confirmButton },
                    },
                },
            },
        };

        // Record the choice before closing. Window.Close() raises Closed synchronously, so the
        // Closed handler must read the already-decided result rather than overwrite it.
        var confirmed = false;
        cancelButton.Click += (_, _) => { confirmed = false; dialog.Close(); };
        confirmButton.Click += (_, _) => { confirmed = true; dialog.Close(); };

        var tcs = new TaskCompletionSource<bool>();
        dialog.Closed += (_, _) => tcs.TrySetResult(confirmed);

        _ = dialog.ShowDialog(owner);
        return tcs.Task;
    }
}
