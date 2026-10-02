namespace SaveGuard.Core.Models;

/// <summary>
/// Distinguishes routine backups from the safety snapshots taken automatically just before a
/// restore. They are stored separately so the backup history and retention are not polluted by
/// pre-restore safety copies.
/// </summary>
public enum SnapshotKind
{
    Backup,
    Safety,
}
