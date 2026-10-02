using Avalonia.Controls;
using SaveGuard.Core.Models;
using SaveGuard.Core.Services;
using SaveGuard.Desktop.ViewModels;

namespace SaveGuard.Desktop.Views;

public partial class BackupHistoryWindow : Window
{
    public BackupHistoryWindow()
    {
        InitializeComponent();
    }

    public BackupHistoryWindow(IBackupService backupService, IBackupMonitor monitor, IAppLogger logger, BackupTarget target)
        : this()
    {
        var vm = new BackupHistoryViewModel(backupService, monitor, logger, target)
        {
            ConfirmRestoreAsync = ConfirmRestoreAsync,
            ShowMessageAsync = ShowMessageAsync,
        };
        DataContext = vm;
    }

    private Task<bool> ConfirmRestoreAsync(Snapshot snapshot)
    {
        var text = $"Restore the backup from {snapshot.CreatedAt.ToLocalTime():MMMM d, yyyy h:mm tt}?\n\n"
                   + "Your current save will be replaced. A safety backup of the current save will be created first.";
        return ConfirmDialog.ShowAsync(this, "Confirm Restore", text);
    }

    private Task ShowMessageAsync(string message) => MessageDialog.ShowAsync(this, "GameSaveGuard", message);
}
