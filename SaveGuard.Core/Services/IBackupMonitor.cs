using SaveGuard.Core.Models;

namespace SaveGuard.Core.Services;

/// <summary>
/// Watches protected save directories and triggers debounced backups when their contents change.
/// A periodic reconciliation pass ensures a FileSystemWatcher miss does not leave a save unprotected.
/// </summary>
public interface IBackupMonitor : IDisposable
{
    /// <summary>Begins monitoring the given target. Safe to call again to replace an existing watch.</summary>
    void Start(BackupTarget target);

    /// <summary>Stops monitoring the given target.</summary>
    void Stop(Guid targetId);

    /// <summary>Stops monitoring all targets.</summary>
    void StopAll();

    /// <summary>
    /// Runs <paramref name="action"/> with monitoring for the target suspended, then restarts it.
    /// Use this around a restore so the watcher does not react to our own writes and does not hold
    /// a handle on the save directory while it is being rewritten.
    /// </summary>
    Task RunWithMonitoringPausedAsync(BackupTarget target, Func<Task> action);

    /// <summary>Raised after a backup triggered by monitoring completes (successfully or not).</summary>
    event EventHandler<BackupCompletedEventArgs>? BackupCompleted;
}

public sealed class BackupCompletedEventArgs : EventArgs
{
    public required Guid TargetId { get; init; }

    public Snapshot? Snapshot { get; init; }

    public Exception? Error { get; init; }

    public bool Succeeded => Error is null && Snapshot is not null;
}
