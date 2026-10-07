using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveGrave.Core.Models;
using SaveGrave.Core.Services;

namespace SaveGrave.Desktop.ViewModels;

/// <summary>
/// Game-details / recovery-point view shown in the main window. Lists recovery points (backups)
/// and safety snapshots newest-first and performs restores. Restore confirmation and user messages
/// are delegated to the host via callbacks so the view layer owns presentation.
/// </summary>
public partial class GameDetailsViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly IBackupMonitor _monitor;
    private readonly IAppLogger _logger;

    public GameDetailsViewModel(
        IBackupService backupService,
        IBackupMonitor monitor,
        IAppLogger logger,
        GameItemViewModel game,
        Action onBack)
    {
        _backupService = backupService;
        _monitor = monitor;
        _logger = logger;
        Game = game;
        OnBack = onBack;
        Reload();
    }

    public GameItemViewModel Game { get; }

    private Action OnBack { get; }

    /// <summary>Set by the host: shows a confirmation prompt, returns true to proceed with the restore.</summary>
    public Func<Snapshot, Task<bool>>? ConfirmRestoreAsync { get; set; }

    /// <summary>Set by the host: confirms deleting a single recovery point.</summary>
    public Func<Snapshot, Task<bool>>? ConfirmDeleteAsync { get; set; }

    /// <summary>Set by the host: surfaces a completion or error message to the user.</summary>
    public Func<string, Task>? ShowMessageAsync { get; set; }

    /// <summary>Set by the host: opens a folder in the OS file manager.</summary>
    public Action<string>? OpenFolder { get; set; }

    /// <summary>Set by the host: copies text to the clipboard.</summary>
    public Func<string, Task>? CopyToClipboardAsync { get; set; }

    public string DisplayName => Game.DisplayName;

    public string SourcePath => Game.SourcePath;

    public string StorageUsageValue => SaveGrave.Core.ByteSize.Format(_backupService.GetStorageUsage(Game.Target).TotalBytes);

    public ObservableCollection<SnapshotItemViewModel> Snapshots { get; } = [];

    public ObservableCollection<SnapshotItemViewModel> SafetySnapshots { get; } = [];

    public ObservableCollection<SnapshotItemViewModel> ManualSnapshots { get; } = [];

    public bool HasSnapshots => Snapshots.Count > 0;

    public bool HasSafetySnapshots => SafetySnapshots.Count > 0;

    public bool HasManualSnapshots => ManualSnapshots.Count > 0;

    public string BackupCountValue => Snapshots.Count.ToString();

    public string ManualCountValue => ManualSnapshots.Count.ToString();

    public string SafetyCountValue => SafetySnapshots.Count.ToString();

    public string LastBackupValue => Game.LastBackupValue;

    public string LastCheckedValue => Game.LastCheckedValue;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Re-raises relative-time labels so "x ago" stays current without a reload.</summary>
    public void RefreshRelativeTimes()
    {
        OnPropertyChanged(nameof(LastBackupValue));
        OnPropertyChanged(nameof(LastCheckedValue));
    }

    [RelayCommand]
    private void Back() => OnBack();

    [RelayCommand]
    private async Task TakeSnapshot()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var snapshot = await _backupService.TakeManualSnapshotAsync(Game.Target);
            Reload();
            if (ShowMessageAsync is not null)
            {
                await ShowMessageAsync(snapshot is null
                    ? "A backup is already in progress. Try again in a moment."
                    : "Manual snapshot created.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Manual snapshot failed for '{Game.DisplayName}' at the UI layer.", ex);
            if (ShowMessageAsync is not null)
                await ShowMessageAsync($"Snapshot failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Restore(SnapshotItemViewModel? item)
    {
        if (item is null)
        {
            _logger.Warn($"Restore clicked for '{Game.DisplayName}' but no snapshot was supplied (command parameter was null).");
            return;
        }

        if (IsBusy)
        {
            _logger.Info($"Restore clicked for '{Game.DisplayName}' while another operation was in progress; ignored.");
            return;
        }

        _logger.Info($"Restore requested for '{Game.DisplayName}': snapshot '{item.Snapshot.Path}'.");

        if (ConfirmRestoreAsync is not null && !await ConfirmRestoreAsync(item.Snapshot))
        {
            _logger.Info($"Restore for '{Game.DisplayName}' was cancelled by the user at the confirmation prompt.");
            return;
        }

        IsBusy = true;
        try
        {
            await _monitor.RunWithMonitoringPausedAsync(
                Game.Target,
                () => _backupService.RestoreAsync(Game.Target, item.Snapshot));
            Reload();
            if (ShowMessageAsync is not null)
                await ShowMessageAsync("Restore completed. A safety snapshot of your previous save was created first.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Restore failed for '{Game.DisplayName}' at the UI layer.", ex);
            if (ShowMessageAsync is not null)
                await ShowMessageAsync($"Restore failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenSnapshotFolder(SnapshotItemViewModel? item)
    {
        if (item is not null)
            OpenFolder?.Invoke(item.Path);
    }

    [RelayCommand]
    private async Task CopySnapshotPath(SnapshotItemViewModel? item)
    {
        if (item is not null && CopyToClipboardAsync is not null)
            await CopyToClipboardAsync(item.Path);
    }

    [RelayCommand]
    private async Task DeleteSnapshot(SnapshotItemViewModel? item)
    {
        if (item is null || IsBusy)
            return;

        if (ConfirmDeleteAsync is not null && !await ConfirmDeleteAsync(item.Snapshot))
            return;

        IsBusy = true;
        try
        {
            await _backupService.DeleteSnapshotAsync(Game.Target, item.Snapshot);
            Reload();
            if (ShowMessageAsync is not null)
                await ShowMessageAsync("Recovery point deleted.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Delete snapshot failed for '{Game.DisplayName}' at the UI layer.", ex);
            if (ShowMessageAsync is not null)
                await ShowMessageAsync($"Delete failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Reload()
    {
        Snapshots.Clear();
        foreach (var snapshot in _backupService.GetSnapshots(Game.Target, SnapshotKind.Backup))
            Snapshots.Add(new SnapshotItemViewModel(snapshot, "Automatic backup", this));

        SafetySnapshots.Clear();
        foreach (var snapshot in _backupService.GetSnapshots(Game.Target, SnapshotKind.Safety))
            SafetySnapshots.Add(new SnapshotItemViewModel(snapshot, "Created before restore", this));

        ManualSnapshots.Clear();
        foreach (var snapshot in _backupService.GetSnapshots(Game.Target, SnapshotKind.Manual))
            ManualSnapshots.Add(new SnapshotItemViewModel(snapshot, "Manual snapshot", this));

        // Keep the library card's stats in sync with what the details view shows. "Last backup" is
        // the most recent of an automatic backup or a manual snapshot.
        Game.BackupCount = Snapshots.Count;
        Game.ManualCount = ManualSnapshots.Count;
        Game.SafetyCount = SafetySnapshots.Count;
        Game.StorageBytes = _backupService.GetStorageUsage(Game.Target).TotalBytes;
        DateTimeOffset? newestBackup = Snapshots.Count > 0 ? Snapshots[0].Snapshot.CreatedAt : null;
        DateTimeOffset? newestManual = ManualSnapshots.Count > 0 ? ManualSnapshots[0].Snapshot.CreatedAt : null;
        Game.LastBackupAt = newestManual is null || (newestBackup is not null && newestBackup > newestManual)
            ? newestBackup
            : newestManual;

        OnPropertyChanged(nameof(HasSnapshots));
        OnPropertyChanged(nameof(HasSafetySnapshots));
        OnPropertyChanged(nameof(HasManualSnapshots));
        OnPropertyChanged(nameof(BackupCountValue));
        OnPropertyChanged(nameof(ManualCountValue));
        OnPropertyChanged(nameof(SafetyCountValue));
        OnPropertyChanged(nameof(LastBackupValue));
        OnPropertyChanged(nameof(StorageUsageValue));
    }
}
