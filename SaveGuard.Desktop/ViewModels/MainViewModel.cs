using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveGuard.Core.Models;
using SaveGuard.Core.Services;
using SaveGuard.Infrastructure;

namespace SaveGuard.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IBackupTargetStore _store;
    private readonly IBackupService _backupService;
    private readonly IBackupMonitor _monitor;
    private readonly IAppLogger _logger;
    private readonly List<BackupTarget> _targets = [];

    public MainViewModel()
        : this(new JsonBackupTargetStore(), BuildDefaultServices())
    {
    }

    public MainViewModel(IBackupTargetStore store, (IBackupService Backup, IBackupMonitor Monitor, IAppLogger Logger) services)
    {
        _store = store;
        _backupService = services.Backup;
        _monitor = services.Monitor;
        _logger = services.Logger;
        _monitor.BackupCompleted += OnBackupCompleted;
    }

    /// <summary>Set by the view: opens the Add Game dialog and returns a new target, or null if cancelled.</summary>
    public Func<Task<BackupTarget?>>? ShowAddGameDialogAsync { get; set; }

    /// <summary>Set by the view: opens the backup-history window for a target.</summary>
    public Func<BackupTarget, Task>? ShowBackupHistoryAsync { get; set; }

    /// <summary>Set by the view: asks the user to confirm removing a protected game.</summary>
    public Func<GameItemViewModel, Task<bool>>? ConfirmRemoveAsync { get; set; }

    public ObservableCollection<GameItemViewModel> Games { get; } = [];

    public bool HasGames => Games.Count > 0;

    public IBackupService BackupService => _backupService;

    public IBackupMonitor Monitor => _monitor;

    public IAppLogger Logger => _logger;

    public async Task InitializeAsync()
    {
        var loaded = await _store.LoadAsync();
        _logger.Info($"Loaded {loaded.Count} protected game(s) from configuration.");
        foreach (var target in loaded)
        {
            _targets.Add(target);
            var item = new GameItemViewModel(target);

            // A target saved before overlap validation existed may have a backup location inside its
            // save folder (or vice versa). Such a target cannot be backed up safely; flag it instead
            // of letting backups fail silently.
            if (SaveGuard.Core.PathUtilities.Overlaps(target.SourcePath, target.BackupPath))
            {
                item.Status = "Misconfigured: backup folder overlaps the save folder. Remove and re-add it.";
                _logger.Warn($"Target '{target.DisplayName}' ({target.Id}) is misconfigured: backup path '{target.BackupPath}' overlaps save path '{target.SourcePath}'. Monitoring not started.");
                Games.Add(item);
                continue;
            }

            RefreshStats(item);
            Games.Add(item);
            _monitor.Start(target);
        }

        OnPropertyChanged(nameof(HasGames));
    }

    [RelayCommand]
    private async Task AddGame()
    {
        if (ShowAddGameDialogAsync is null)
            return;

        var target = await ShowAddGameDialogAsync();
        if (target is null)
            return;

        _targets.Add(target);
        await _store.SaveAsync(_targets);

        var item = new GameItemViewModel(target);
        Games.Add(item);
        OnPropertyChanged(nameof(HasGames));

        // Capture an initial baseline snapshot immediately so the game is protected from the moment
        // it is added, rather than waiting for the first file change.
        await CreateInitialSnapshotAsync(item);

        RefreshStats(item);
        _monitor.Start(target);
    }

    private async Task CreateInitialSnapshotAsync(GameItemViewModel item)
    {
        try
        {
            var snapshot = await _backupService.BackupAsync(item.Target);
            if (snapshot is null)
                _logger.Info($"Initial snapshot for '{item.Target.DisplayName}' was skipped (a backup was already running).");
        }
        catch (Exception ex)
        {
            item.Status = $"Initial backup failed: {ex.Message}";
            _logger.Error($"Initial snapshot failed for '{item.Target.DisplayName}' ({item.Target.Id}).", ex);
        }
    }

    [RelayCommand]
    private async Task ViewBackups(GameItemViewModel? item)
    {
        if (item is null || ShowBackupHistoryAsync is null)
            return;

        await ShowBackupHistoryAsync(item.Target);
        RefreshStats(item);
    }

    [RelayCommand]
    private async Task RemoveGame(GameItemViewModel? item)
    {
        if (item is null)
            return;

        if (ConfirmRemoveAsync is not null && !await ConfirmRemoveAsync(item))
            return;

        // Removing a protected game stops monitoring but never touches the save or its backups.
        _monitor.Stop(item.Id);
        _targets.RemoveAll(t => t.Id == item.Id);
        Games.Remove(item);
        OnPropertyChanged(nameof(HasGames));

        await _store.SaveAsync(_targets);
    }

    public void Shutdown()
    {
        _monitor.BackupCompleted -= OnBackupCompleted;
        _monitor.Dispose();
    }

    private void OnBackupCompleted(object? sender, BackupCompletedEventArgs e)
    {
        // Monitor callbacks arrive on a background thread; marshal UI updates onto the UI thread.
        Dispatcher.UIThread.Post(() =>
        {
            var item = Games.FirstOrDefault(g => g.Id == e.TargetId);
            if (item is null)
                return;

            if (e.Succeeded)
            {
                item.Status = "Protected";
                RefreshStats(item);
            }
            else
            {
                item.Status = $"Backup failed: {e.Error?.Message}";
            }
        });
    }

    private void RefreshStats(GameItemViewModel item)
    {
        var snapshots = _backupService.GetSnapshots(item.Target);
        item.BackupCount = snapshots.Count;
        item.LastBackupAt = snapshots.Count > 0 ? snapshots[0].CreatedAt : null;
    }

    private static (IBackupService, IBackupMonitor, IAppLogger) BuildDefaultServices()
    {
        var logger = new FileLogger();
        var snapshotService = new SnapshotService();
        var backupService = new BackupService(snapshotService, logger);
        var monitor = new FileSystemBackupMonitor(backupService, snapshotService, logger);
        logger.Info("GameSaveGuard services initialized.");
        return (backupService, monitor, logger);
    }
}
