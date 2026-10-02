using Avalonia.Controls;
using Avalonia.Layout;

namespace SaveGuard.Desktop.Views;

/// <summary>Minimal informational dialog with a single OK button, built in code to avoid extra XAML.</summary>
internal static class MessageDialog
{
    public static Task ShowAsync(Window owner, string title, string message)
    {
        var okButton = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };

        var dialog = new Window
        {
            Title = title,
            Width = 420,
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
                    okButton,
                },
            },
        };

        okButton.Click += (_, _) => dialog.Close();
        return dialog.ShowDialog(owner);
    }
}
