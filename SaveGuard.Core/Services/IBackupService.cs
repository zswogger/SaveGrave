using SaveGuard.Core.Models;

namespace SaveGuard.Core.Services;

/// <summary>
/// Orchestrates backups for a target: creating a snapshot then applying retention.
/// Prevents overlapping backups for the same target and handles restore with a safety snapshot.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a snapshot of the target's save directory, then prunes old snapshots down to
    /// <see cref="BackupTarget.MaxBackups"/>. Returns the created snapshot, or null if a backup
    /// for this target was already running (the request is coalesced).
    /// </summary>
    Task<Snapshot?> BackupAsync(BackupTarget target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a manual, on-demand snapshot of the target's save directory, then prunes old manual
    /// snapshots down to <see cref="BackupTarget.MaxBackups"/>. Returns the created snapshot, or null
    /// if another backup for this target was already running (the request is coalesced).
    /// </summary>
    Task<Snapshot?> TakeManualSnapshotAsync(BackupTarget target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the given snapshot onto the target's save directory. Before replacing anything,
    /// creates a safety snapshot of the current save directory if it exists and contains data.
    /// </summary>
    Task RestoreAsync(BackupTarget target, Snapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns successful snapshots of the given kind for a target, ordered newest to oldest.
    /// Safety snapshots are the automatic copies taken just before a restore.
    /// </summary>
    IReadOnlyList<Snapshot> GetSnapshots(BackupTarget target, SnapshotKind kind = SnapshotKind.Backup);

    /// <summary>Permanently deletes a single snapshot directory for the given target.</summary>
    Task DeleteSnapshotAsync(BackupTarget target, Snapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Computes on-disk storage used by the target's snapshots, broken down by category.</summary>
    StorageUsage GetStorageUsage(BackupTarget target);
}
