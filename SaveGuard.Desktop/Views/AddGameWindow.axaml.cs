using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaveGuard.Core.Models;
using SaveGuard.Desktop.ViewModels;

namespace SaveGuard.Desktop.Views;

public partial class AddGameWindow : Window
{
    public AddGameWindow()
    {
        InitializeComponent();

        var vm = new AddGameViewModel { PickFolderAsync = PickFolderAsync };
        DataContext = vm;
    }

    private AddGameViewModel ViewModel => (AddGameViewModel)DataContext!;

    private async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void OnProtect(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.TryBuildTarget())
            Close(ViewModel.Result);
    }

    public Task<BackupTarget?> ShowDialogAsync(Window owner) => ShowDialog<BackupTarget?>(owner);
}
