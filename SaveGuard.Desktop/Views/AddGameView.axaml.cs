using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaveGuard.Core.Models;
using SaveGuard.Desktop.ViewModels;

namespace SaveGuard.Desktop.Views;

public partial class AddGameView : UserControl
{
    private readonly TaskCompletionSource<BackupTarget?> _completion = new();

    public AddGameView()
    {
        InitializeComponent();
        DataContext = new AddGameViewModel { PickFolderAsync = PickFolderAsync };
    }

    private AddGameViewModel ViewModel => (AddGameViewModel)DataContext!;

    /// <summary>Completes when the user confirms (target) or cancels (null).</summary>
    public Task<BackupTarget?> Completion => _completion.Task;

    private async Task<string?> PickFolderAsync(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => _completion.TrySetResult(null);

    private void OnProtect(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.TryBuildTarget())
            _completion.TrySetResult(ViewModel.Result);
    }
}
