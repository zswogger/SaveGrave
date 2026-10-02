using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaveGuard.Desktop.ViewModels;

namespace SaveGuard.Desktop.Views;

public partial class SettingsView : UserControl
{
    private readonly TaskCompletionSource _completion = new();

    public SettingsView()
    {
        InitializeComponent();
    }

    public SettingsView(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    /// <summary>Completes when the dialog is dismissed.</summary>
    public Task Completion => _completion.Task;

    private void OnDone(object? sender, RoutedEventArgs e) => _completion.TrySetResult();
}
