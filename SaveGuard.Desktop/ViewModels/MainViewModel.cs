using System.Collections.ObjectModel;
using System.Diagnostics;
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

    private readonly DispatcherTimer _relativeTimeTimer;

    public MainViewModel(IBackupTargetStore store, (IBackupService Backup, IBackupMonitor Monitor, IAppLogger Logger) services)
    {
        _store = store;
        _backupService = services.Backup;
        _monitor = services.Monitor;
        _logger = services.Logger;
        _monitor.BackupCompleted += OnBackupCompleted;

        // Refresh relative "x minutes ago" labels periodically so they stay accurate without a
        // new backup. Purely presentational; touches no backup state.
        _relativeTimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _relativeTimeTimer.Tick += (_, _) => RefreshRelativeTimes();
        _relativeTimeTimer.Start();
    }

    private void RefreshRelativeTimes()
    {
        foreach (var game in Games)
            game.RefreshRelativeTimes();
        CurrentDetails?.RefreshRelativeTimes();
    }

    // --- View-provided interaction hooks (set by MainWindow). ---

    /// <summary>Opens the Add Game dialog and returns a new target, or null if cancelled.</summary>
    public Func<Task<BackupTarget?>>? ShowAddGameDialogAsync { get; set; }

    /// <summary>Asks the user to confirm removing a protected game.</summary>
    public Func<GameItemViewModel, Task<bool>>? ConfirmRemoveAsync { get; set; }

    /// <summary>Confirms a restore for the given snapshot.</summary>
    public Func<Snapshot, Task<bool>>? ConfirmRestoreAsync { get; set; }

    /// <summary>Shows a transient/standard message to the user.</summary>
    public Func<string, Task>? ShowMessageAsync { get; set; }

    // --- Library state ---

    public ObservableCollection<GameItemViewModel> Games { get; } = [];

    public bool HasGames => Games.Count > 0;

    // --- Navigation state ---

    [ObservableProperty]
    public partial GameDetailsViewModel? CurrentDetails { get; set; }

    public bool IsShowingDetails => CurrentDetails is not null;

    public bool IsShowingLibrary => CurrentDetails is null;

    partial void OnCurrentDetailsChanged(GameDetailsViewModel? value)
    {
        OnPropertyChanged(nameof(IsShowingDetails));
        OnPropertyChanged(nameof(IsShowingLibrary));
    }

    public async Task InitializeAsync()
    {
        var loaded = await _store.LoadAsync();
        _logger.Info($"Loaded {loaded.Count} protected game(s) from configuration.");
        foreach (var target in loaded)
        {
            _targets.Add(target);
            var item = new GameItemViewModel(target) { Owner = this };

            if (SaveGuard.Core.PathUtilities.Overlaps(target.SourcePath, target.BackupPath))
            {
                item.State = ProtectionState.Error;
                item.Status = "Backup folder overlaps the save folder. Remove and re-add it.";
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

        var item = new GameItemViewModel(target) { Owner = this };
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
            item.State = ProtectionState.Error;
            item.Status = $"Initial backup failed: {ex.Message}";
            _logger.Error($"Initial snapshot failed for '{item.Target.DisplayName}' ({item.Target.Id}).", ex);
        }
    }

    [RelayCommand]
    private void ViewBackups(GameItemViewModel? item)
    {
        if (item is null)
            return;

        var details = new GameDetailsViewModel(_backupService, _monitor, _logger, item, onBack: ShowLibrary)
        {
            ConfirmRestoreAsync = ConfirmRestoreAsync,
            ShowMessageAsync = ShowMessageAsync,
        };
        CurrentDetails = details;
    }

    private void ShowLibrary() => CurrentDetails = null;

    [RelayCommand]
    private async Task TakeSnapshot(GameItemViewModel? item)
    {
        if (item is null || item.State == ProtectionState.Error)
            return;

        var previousState = item.State;
        item.State = ProtectionState.BackingUp;
        try
        {
            var snapshot = await _backupService.TakeManualSnapshotAsync(item.Target);
            RefreshStats(item);
            item.State = previousState == ProtectionState.BackingUp ? ProtectionState.Protected : previousState;

            if (ShowMessageAsync is not null)
            {
                await ShowMessageAsync(snapshot is null
                    ? "A backup is already in progress. Try again in a moment."
                    : $"Manual snapshot created for \"{item.DisplayName}\".");
            }
        }
        catch (Exception ex)
        {
            item.State = ProtectionState.Error;
            item.Status = $"Snapshot failed: {ex.Message}";
            _logger.Error($"Manual snapshot failed for '{item.DisplayName}' ({item.Id}).", ex);
            if (ShowMessageAsync is not null)
                await ShowMessageAsync($"Snapshot failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenSaveFolder(GameItemViewModel? item) => OpenFolder(item?.SourcePath);

    [RelayCommand]
    private void OpenBackupFolder(GameItemViewModel? item) => OpenFolder(item?.BackupPath);

    [RelayCommand]
    private void TogglePause(GameItemViewModel? item)
    {
        if (item is null || item.State == ProtectionState.Error)
            return;

        if (item.State == ProtectionState.Paused)
        {
            _monitor.Start(item.Target);
            item.State = ProtectionState.Protected;
            _logger.Info($"Protection resumed for '{item.DisplayName}'.");
        }
        else
        {
            _monitor.Stop(item.Id);
            item.State = ProtectionState.Paused;
            _logger.Info($"Protection paused for '{item.DisplayName}'.");
        }
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

        if (CurrentDetails?.Game.Id == item.Id)
            ShowLibrary();

        await _store.SaveAsync(_targets);
    }

    public void Shutdown()
    {
        _relativeTimeTimer.Stop();
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
                if (item.State != ProtectionState.Paused)
                    item.State = ProtectionState.Protected;
                item.Status = "Protected";
                RefreshStats(item);
            }
            else
            {
                item.State = ProtectionState.Error;
                item.Status = $"Backup failed: {e.Error?.Message}";
            }
        });
    }

    private void RefreshStats(GameItemViewModel item)
    {
        var backups = _backupService.GetSnapshots(item.Target, SnapshotKind.Backup);
        var manual = _backupService.GetSnapshots(item.Target, SnapshotKind.Manual);
        item.BackupCount = backups.Count;
        item.ManualCount = manual.Count;
        item.SafetyCount = _backupService.GetSnapshots(item.Target, SnapshotKind.Safety).Count;

        // "Last backup" reflects the most recent protective snapshot the user initiated or that
        // monitoring created - i.e. the newest of an automatic backup or a manual snapshot.
        item.LastBackupAt = MostRecent(
            backups.Count > 0 ? backups[0].CreatedAt : null,
            manual.Count > 0 ? manual[0].CreatedAt : null);
    }

    private static DateTimeOffset? MostRecent(DateTimeOffset? a, DateTimeOffset? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return a > b ? a : b;
    }

    private void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (!Directory.Exists(path))
            {
                _logger.Warn($"Cannot open folder; it does not exist: '{path}'.");
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to open folder '{path}'.", ex);
        }
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
