using Avalonia.Controls;
using SaveGuard.Core.Models;
using SaveGuard.Desktop.ViewModels;

namespace SaveGuard.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (ViewModel is not { } vm)
            return;

        vm.ShowAddGameDialogAsync = ShowAddGameDialogAsync;
        vm.ShowBackupHistoryAsync = ShowBackupHistoryAsync;
        vm.ConfirmRemoveAsync = ConfirmRemoveAsync;
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        if (ViewModel is { } vm)
            await vm.InitializeAsync();
    }

    private void OnClosed(object? sender, System.EventArgs e) => ViewModel?.Shutdown();

    private Task<BackupTarget?> ShowAddGameDialogAsync()
    {
        var dialog = new AddGameWindow();
        return dialog.ShowDialogAsync(this);
    }

    private Task ShowBackupHistoryAsync(BackupTarget target)
    {
        if (ViewModel is not { } vm)
            return Task.CompletedTask;

        var window = new BackupHistoryWindow(vm.BackupService, vm.Monitor, vm.Logger, target);
        return window.ShowDialog(this);
    }

    private Task<bool> ConfirmRemoveAsync(GameItemViewModel item)
    {
        var text = $"Remove \"{item.DisplayName}\" from GameSaveGuard?\n\n"
                   + "This stops monitoring but does not delete your saves or existing backups.";
        return ConfirmDialog.ShowAsync(this, "Remove Game", text);
    }
}
