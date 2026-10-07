using System.Collections.Concurrent;
using SaveGrave.Core.Models;
using SaveGrave.Core.Services;

namespace SaveGrave.Infrastructure;

/// <summary>
/// Orchestrates snapshot creation and retention for a target, serializing backups per target so
/// two backups for the same target never run concurrently. Restore first creates a safety snapshot
/// of the current save directory.
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly ISnapshotService _snapshotService;
    private readonly IAppLogger _logger;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public BackupService(ISnapshotService snapshotService, IAppLogger logger)
    {
        _snapshotService = snapshotService;
        _logger = logger;
    }

    public async Task<Snapshot?> BackupAsync(BackupTarget target, CancellationToken cancellationToken = default)
    {
        var gate = _locks.GetOrAdd(target.Id, _ => new SemaphoreSlim(1, 1));

        // If a backup for this target is already running, coalesce this request rather than queueing
        // a redundant one. The monitor will re-trigger if the directory is still dirty afterward.
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return null;

        try
        {
            _logger.Info($"Backup starting for '{target.DisplayName}' ({target.Id}): source '{target.SourcePath}' -> '{target.BackupPath}'.");
            var snapshot = await _snapshotService
                .CreateSnapshotAsync(target.Id, target.SourcePath, target.BackupPath, SnapshotKind.Backup, cancellationToken)
                .ConfigureAwait(false);

            ApplyRetention(target, SnapshotKind.Backup);
            _logger.Info($"Backup completed for '{target.DisplayName}': snapshot '{snapshot.Path}' ({snapshot.SizeBytes} bytes).");
            if (snapshot.SkippedFiles.Count > 0)
            {
                _logger.Warn($"Backup for '{target.DisplayName}' skipped {snapshot.SkippedFiles.Count} locked file(s) " +
                             $"(still in use by another process): {string.Join(", ", snapshot.SkippedFiles)}. " +
                             "These will be captured on a later backup once the lock is released.");
            }
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.Error($"Backup failed for '{target.DisplayName}' ({target.Id}).", ex);
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Snapshot?> TakeManualSnapshotAsync(BackupTarget target, CancellationToken cancellationToken = default)
    {
        var gate = _locks.GetOrAdd(target.Id, _ => new SemaphoreSlim(1, 1));

        // Coalesce with any in-flight backup for this target, same as automatic backups.
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return null;

        try
        {
            _logger.Info($"Manual snapshot starting for '{target.DisplayName}' ({target.Id}).");
            var snapshot = await _snapshotService
                .CreateSnapshotAsync(target.Id, target.SourcePath, target.BackupPath, SnapshotKind.Manual, cancellationToken)
                .ConfigureAwait(false);

            ApplyRetention(target, SnapshotKind.Manual);
            _logger.Info($"Manual snapshot completed for '{target.DisplayName}': '{snapshot.Path}' ({snapshot.SizeBytes} bytes).");
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.Error($"Manual snapshot failed for '{target.DisplayName}' ({target.Id}).", ex);
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RestoreAsync(BackupTarget target, Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        var gate = _locks.GetOrAdd(target.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.Info($"Restore starting for '{target.DisplayName}' ({target.Id}): snapshot '{snapshot.Path}' -> save '{target.SourcePath}'.");

            // Protect the user's current save before we overwrite it. Only skip when there is
            // genuinely nothing to lose (missing or empty save directory).
            if (CurrentSaveHasData(target.SourcePath))
            {
                _logger.Info($"Creating safety snapshot of current save for '{target.DisplayName}' before restore.");
                await _snapshotService
                    .CreateSnapshotAsync(target.Id, target.SourcePath, target.BackupPath, SnapshotKind.Safety, cancellationToken)
                    .ConfigureAwait(false);
                ApplyRetention(target, SnapshotKind.Safety);
            }
            else
            {
                _logger.Info($"No safety snapshot needed for '{target.DisplayName}': current save is missing or empty.");
            }

            await _snapshotService
                .RestoreSnapshotAsync(snapshot, target.SourcePath, cancellationToken)
                .ConfigureAwait(false);

            _logger.Info($"Restore completed for '{target.DisplayName}'.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Restore failed for '{target.DisplayName}' ({target.Id}): snapshot '{snapshot.Path}' -> save '{target.SourcePath}'.", ex);
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public IReadOnlyList<Snapshot> GetSnapshots(BackupTarget target, SnapshotKind kind = SnapshotKind.Backup)
    {
        var snapshots = _snapshotService.GetSnapshots(target.Id, target.BackupPath, kind).ToList();
        snapshots.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return snapshots;
    }

    public async Task DeleteSnapshotAsync(BackupTarget target, Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        var gate = _locks.GetOrAdd(target.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.Info($"Deleting snapshot for '{target.DisplayName}' ({target.Id}): '{snapshot.Path}'.");
            _snapshotService.DeleteSnapshot(snapshot);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to delete snapshot '{snapshot.Path}' for '{target.DisplayName}'.", ex);
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public StorageUsage GetStorageUsage(BackupTarget target)
    {
        return new StorageUsage(
            BackupBytes: SumSizes(target, SnapshotKind.Backup),
            ManualBytes: SumSizes(target, SnapshotKind.Manual),
            SafetyBytes: SumSizes(target, SnapshotKind.Safety));
    }

    private long SumSizes(BackupTarget target, SnapshotKind kind)
    {
        long total = 0;
        foreach (var snapshot in _snapshotService.GetSnapshots(target.Id, target.BackupPath, kind))
            total += snapshot.SizeBytes;
        return total;
    }

    private void ApplyRetention(BackupTarget target, SnapshotKind kind)
    {
        if (target.MaxBackups <= 0)
            return;

        // Oldest-to-newest; delete from the front until we are within the cap. The newest snapshot
        // is always at the end and is never removed here. Backups and safety snapshots are pruned
        // independently so a restore's safety copies never evict normal backup history.
        var snapshots = _snapshotService.GetSnapshots(target.Id, target.BackupPath, kind);
        var excess = snapshots.Count - target.MaxBackups;
        for (var i = 0; i < excess; i++)
        {
            _snapshotService.DeleteSnapshot(snapshots[i]);
        }
    }

    private static bool CurrentSaveHasData(string sourcePath)
    {
        return Directory.Exists(sourcePath)
               && Directory.EnumerateFileSystemEntries(sourcePath).Any();
    }
}
