using System.Collections.Concurrent;
using SaveGuard.Core.Models;
using SaveGuard.Core.Services;

namespace SaveGuard.Infrastructure;

/// <summary>
/// Monitors protected save directories with a <see cref="FileSystemWatcher"/>, debouncing bursts of
/// change events so a single save operation produces a single snapshot. A periodic reconciliation
/// timer provides a safety net in case watcher events are missed.
/// </summary>
public sealed class FileSystemBackupMonitor : IBackupMonitor
{
    private readonly IBackupService _backupService;
    private readonly ISnapshotService _snapshotService;
    private readonly IAppLogger? _logger;
    private readonly TimeSpan _debounceDelay;
    private readonly TimeSpan _reconcileInterval;
    private readonly ConcurrentDictionary<Guid, MonitoredTarget> _targets = new();
    private bool _disposed;

    public FileSystemBackupMonitor(
        IBackupService backupService,
        ISnapshotService snapshotService,
        IAppLogger? logger = null,
        TimeSpan? debounceDelay = null,
        TimeSpan? reconcileInterval = null)
    {
        _backupService = backupService;
        _snapshotService = snapshotService;
        _logger = logger;
        _debounceDelay = debounceDelay ?? TimeSpan.FromSeconds(10);
        _reconcileInterval = reconcileInterval ?? TimeSpan.FromMinutes(5);
    }

    public event EventHandler<BackupCompletedEventArgs>? BackupCompleted;

    public void Start(BackupTarget target)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Stop(target.Id);

        if (!target.IsEnabled)
            return;

        var monitored = new MonitoredTarget(target, this);
        _targets[target.Id] = monitored;
        monitored.Begin();
    }

    public void Stop(Guid targetId)
    {
        if (_targets.TryRemove(targetId, out var monitored))
            monitored.Dispose();
    }

    public void StopAll()
    {
        foreach (var id in _targets.Keys.ToArray())
            Stop(id);
    }

    public async Task RunWithMonitoringPausedAsync(BackupTarget target, Func<Task> action)
    {
        var wasMonitoring = _targets.ContainsKey(target.Id);

        // Tearing down the watch disposes the FileSystemWatcher and its timers, releasing the handle
        // on the save directory and cancelling any pending debounce before we touch the directory.
        Stop(target.Id);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            if (wasMonitoring && !_disposed)
                Start(target);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        StopAll();
    }

    private async Task RunBackupAsync(BackupTarget target)
    {
        try
        {
            var snapshot = await _backupService.BackupAsync(target).ConfigureAwait(false);
            // A null snapshot means a backup was already running; the triggering target stays dirty
            // and reconciliation (or a later event) will catch any outstanding changes.
            if (snapshot is not null)
            {
                BackupCompleted?.Invoke(this, new BackupCompletedEventArgs { TargetId = target.Id, Snapshot = snapshot });
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"Monitored backup failed for '{target.DisplayName}' ({target.Id}).", ex);
            BackupCompleted?.Invoke(this, new BackupCompletedEventArgs { TargetId = target.Id, Error = ex });
        }
    }

    private bool NeedsBackup(BackupTarget target)
    {
        var snapshots = _snapshotService.GetSnapshots(target.Id, target.BackupPath);
        if (snapshots.Count == 0)
            return Directory.Exists(target.SourcePath) && Directory.EnumerateFileSystemEntries(target.SourcePath).Any();

        var newest = snapshots[^1];
        return _snapshotService.DiffersFromSnapshot(target.SourcePath, newest);
    }

    private sealed class MonitoredTarget : IDisposable
    {
        private readonly BackupTarget _target;
        private readonly FileSystemBackupMonitor _owner;
        private readonly object _sync = new();
        private FileSystemWatcher? _watcher;
        private Timer? _debounceTimer;
        private Timer? _reconcileTimer;
        private bool _dirty;
        private bool _disposed;

        public MonitoredTarget(BackupTarget target, FileSystemBackupMonitor owner)
        {
            _target = target;
            _owner = owner;
        }

        public void Begin()
        {
            if (Directory.Exists(_target.SourcePath))
            {
                _watcher = new FileSystemWatcher(_target.SourcePath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                                   | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                };
                _watcher.Changed += OnChanged;
                _watcher.Created += OnChanged;
                _watcher.Deleted += OnChanged;
                _watcher.Renamed += OnChanged;
                _watcher.Error += OnWatcherError;
                _watcher.EnableRaisingEvents = true;
            }
            else
            {
                _owner._logger?.Warn($"Save folder for '{_target.DisplayName}' does not exist; watcher not started: '{_target.SourcePath}'. Reconciliation will still run.");
            }

            _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
            _reconcileTimer = new Timer(OnReconcile, null, _owner._reconcileInterval, _owner._reconcileInterval);
        }

        private void OnChanged(object sender, FileSystemEventArgs e) => MarkDirty();

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            // The watcher's internal buffer can overflow during a large burst of changes, dropping
            // events. Log it and treat the target as dirty so the save is still captured.
            _owner._logger?.Warn($"FileSystemWatcher error for '{_target.DisplayName}' ({_target.Id}); some change events may have been missed: {e.GetException().Message}");
            MarkDirty();
        }

        private void MarkDirty()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
                _dirty = true;
                // Reset the debounce window on every change so a burst of writes collapses into one
                // snapshot taken only after the directory has been quiet.
                _debounceTimer?.Change(_owner._debounceDelay, Timeout.InfiniteTimeSpan);
            }
        }

        private void OnDebounceElapsed(object? state)
        {
            lock (_sync)
            {
                if (_disposed || !_dirty)
                    return;
                _dirty = false;
            }

            _ = _owner.RunBackupAsync(_target);
        }

        private void OnReconcile(object? state)
        {
            bool shouldBackup;
            try
            {
                shouldBackup = _owner.NeedsBackup(_target);
            }
            catch
            {
                return;
            }

            if (shouldBackup)
                _ = _owner.RunBackupAsync(_target);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnChanged;
                _watcher.Created -= OnChanged;
                _watcher.Deleted -= OnChanged;
                _watcher.Renamed -= OnChanged;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
            }

            _debounceTimer?.Dispose();
            _reconcileTimer?.Dispose();
        }
    }
}
