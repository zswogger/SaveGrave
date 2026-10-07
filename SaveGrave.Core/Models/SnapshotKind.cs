namespace SaveGrave.Core.Models;

/// <summary>
/// Distinguishes the three kinds of snapshot Save Grave keeps. Each kind is stored and pruned
/// separately so the categories never interfere with one another:
///   <list type="bullet">
///     <item><description><see cref="Backup"/>: automatic, change-triggered backups.</description></item>
///     <item><description><see cref="Safety"/>: taken automatically just before a restore.</description></item>
///     <item><description><see cref="Manual"/>: created on demand by the user ("Take Snapshot").</description></item>
///   </list>
/// </summary>
public enum SnapshotKind
{
    Backup,
    Safety,
    Manual,
}
