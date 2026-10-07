using SaveGrave.Core.Models;

namespace SaveGrave.Core.Services;

/// <summary>
/// Low-level snapshot mechanics: copying a save directory into a timestamped snapshot
/// directory, enumerating snapshots, deleting them, and restoring one back onto a save
/// directory. Implementations must treat a snapshot as successful only after every file
/// has been copied.
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Copies the complete contents of <paramref name="sourcePath"/> into a new timestamped
    /// snapshot directory for the target. Routine backups and <see cref="SnapshotKind.Safety"/>
    /// snapshots are stored under separate locations. If copying fails, incomplete snapshot data is
    /// cleaned up and the exception propagates.
    /// </summary>
    Task<Snapshot> CreateSnapshotAsync(Guid targetId, string sourcePath, string backupPath, SnapshotKind kind = SnapshotKind.Backup, CancellationToken cancellationToken = default);

    /// <summary>Returns successful snapshots of the given kind for a target, ordered oldest to newest.</summary>
    IReadOnlyList<Snapshot> GetSnapshots(Guid targetId, string backupPath, SnapshotKind kind = SnapshotKind.Backup);

    /// <summary>Permanently deletes a snapshot directory.</summary>
    void DeleteSnapshot(Snapshot snapshot);

    /// <summary>
    /// Replaces the contents of <paramref name="destinationPath"/> with the contents of the
    /// given snapshot, preserving directory structure.
    /// </summary>
    Task RestoreSnapshotAsync(Snapshot snapshot, string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the current contents of <paramref name="sourcePath"/> differ from the
    /// contents of the given snapshot. Used by periodic reconciliation.
    /// </summary>
    bool DiffersFromSnapshot(string sourcePath, Snapshot snapshot);
}
