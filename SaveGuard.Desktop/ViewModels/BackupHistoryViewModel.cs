using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveGuard.Core.Models;
using SaveGuard.Core.Services;

namespace SaveGuard.Desktop.ViewModels;

/// <summary>
/// Lists snapshots for a target newest-first and performs restores. Restore confirmation is
/// delegated to the view via <see cref="ConfirmRestoreAsync"/>.
/// </summary>
public partial class BackupHistoryViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly IBackupMonitor _monitor;
    private readonly IAppLogger _logger;
    private readonly BackupTarget _target;

    public BackupHistoryViewModel(IBackupService backupService, IBackupMonitor monitor, IAppLogger logger, BackupTarget target)
    {
        _backupService = backupService;
        _monitor = monitor;
        _logger = logger;
        _target = target;
        Title = $"Backups: {target.DisplayName}";
        Reload();
    }

    /// <summary>Set by the view: shows a confirmation prompt, returns true to proceed with the restore.</summary>
    public Func<Snapshot, Task<bool>>? ConfirmRestoreAsync { get; set; }

    /// <summary>Set by the view: surfaces a completion or error message to the user.</summary>
    public Func<string, Task>? ShowMessageAsync { get; set; }

    public string Title { get; }

    public ObservableCollection<SnapshotItemViewModel> Snapshots { get; } = [];

    /// <summary>Pre-restore safety copies, restorable as an "undo" of a previous restore.</summary>
    public ObservableCollection<SnapshotItemViewModel> SafetySnapshots { get; } = [];

    public bool HasSafetySnapshots => SafetySnapshots.Count > 0;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task Restore(SnapshotItemViewModel? item)
    {
        if (item is null)
        {
            _logger.Warn($"Restore clicked for '{_target.DisplayName}' but no snapshot was supplied (command parameter was null).");
            return;
        }

        if (IsBusy)
        {
            _logger.Info($"Restore clicked for '{_target.DisplayName}' while another operation was in progress; ignored.");
            return;
        }

        _logger.Info($"Restore requested for '{_target.DisplayName}': snapshot '{item.Snapshot.Path}'.");

        if (ConfirmRestoreAsync is not null && !await ConfirmRestoreAsync(item.Snapshot))
        {
            _logger.Info($"Restore for '{_target.DisplayName}' was cancelled by the user at the confirmation prompt.");
            return;
        }

        IsBusy = true;
        try
        {
            await _monitor.RunWithMonitoringPausedAsync(
                _target,
                () => _backupService.RestoreAsync(_target, item.Snapshot));
            Reload();
            if (ShowMessageAsync is not null)
                await ShowMessageAsync("Restore completed. A safety backup of your previous save was created.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Restore failed for '{_target.DisplayName}' at the UI layer.", ex);
            if (ShowMessageAsync is not null)
                await ShowMessageAsync($"Restore failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Reload()
    {
        Snapshots.Clear();
        foreach (var snapshot in _backupService.GetSnapshots(_target, SnapshotKind.Backup))
            Snapshots.Add(new SnapshotItemViewModel(snapshot));

        SafetySnapshots.Clear();
        foreach (var snapshot in _backupService.GetSnapshots(_target, SnapshotKind.Safety))
            SafetySnapshots.Add(new SnapshotItemViewModel(snapshot));

        OnPropertyChanged(nameof(HasSafetySnapshots));
    }
}
