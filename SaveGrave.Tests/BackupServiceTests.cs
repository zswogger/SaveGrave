using SaveGrave.Core.Models;
using SaveGrave.Core.Services;
using SaveGrave.Infrastructure;

namespace SaveGrave.Tests;

public class BackupServiceTests
{
    [Fact]
    public async Task Backup_CreatesSnapshotForTarget()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();
        source.WriteFile("save.dat", "data");

        var service = new BackupService(new SnapshotService(), new TestLogger());
        var target = NewTarget(source.Path, backup.Path, maxBackups: 10);

        var snapshot = await service.BackupAsync(target);

        Assert.NotNull(snapshot);
        Assert.Single(service.GetSnapshots(target));
    }

    [Fact]
    public async Task Retention_RemovesOldestBeyondMaxBackups()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();
        source.WriteFile("save.dat", "data");

        var service = new BackupService(new SnapshotService(), new TestLogger());
        var target = NewTarget(source.Path, backup.Path, maxBackups: 3);

        for (var i = 0; i < 6; i++)
        {
            source.WriteFile("save.dat", $"data-{i}");
            await service.BackupAsync(target);
        }

        var snapshots = service.GetSnapshots(target);
        Assert.True(snapshots.Count <= target.MaxBackups);
    }

    [Fact]
    public async Task Retention_NeverExceedsMaxBackupsAfterSuccessfulSnapshot()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();

        var service = new BackupService(new SnapshotService(), new TestLogger());
        var target = NewTarget(source.Path, backup.Path, maxBackups: 2);

        for (var i = 0; i < 5; i++)
        {
            source.WriteFile("save.dat", $"v{i}");
            await service.BackupAsync(target);
            Assert.True(service.GetSnapshots(target).Count <= 2);
        }
    }

    [Fact]
    public async Task FailedBackup_DoesNotRemovePreviousSuccessfulSnapshots()
    {
        using var backup = new TempDirectory();
        var fake = new ControllableSnapshotService();
        var service = new BackupService(fake, new TestLogger());
        var target = NewTarget("ignored", backup.Path, maxBackups: 5);

        // Two good snapshots recorded.
        await service.BackupAsync(target);
        await service.BackupAsync(target);
        Assert.Equal(2, service.GetSnapshots(target).Count);

        // Next backup throws; the previous snapshots must remain.
        fake.FailNext = true;
        await Assert.ThrowsAsync<IOException>(() => service.BackupAsync(target));

        Assert.Equal(2, service.GetSnapshots(target).Count);
    }

    [Fact]
    public async Task Backup_WhenAlreadyRunning_IsCoalesced()
    {
        using var backup = new TempDirectory();
        var fake = new ControllableSnapshotService();
        var service = new BackupService(fake, new TestLogger());
        var target = NewTarget("ignored", backup.Path, maxBackups: 10);

        // Hold the first backup inside the snapshot service so the second request overlaps it.
        fake.BlockNext();
        var first = service.BackupAsync(target);
        await fake.WaitUntilInside();

        var second = await service.BackupAsync(target);
        Assert.Null(second); // coalesced because a backup is already running

        fake.Release();
        Assert.NotNull(await first);
    }

    [Fact]
    public async Task Restore_CreatesSafetyBackupOfCurrentSaveFirst()
    {
        using var save = new TempDirectory();
        using var backup = new TempDirectory();
        save.WriteFile("save.dat", "current-save");

        var snapshotService = new SnapshotService();
        var service = new BackupService(snapshotService, new TestLogger());
        var target = NewTarget(save.Path, backup.Path, maxBackups: 10);

        // One snapshot to restore from, taken from a different content state.
        var snapshotToRestore = await service.BackupAsync(target);
        Assert.NotNull(snapshotToRestore);

        // Change the live save after the snapshot so the safety backup captures distinct content.
        save.WriteFile("save.dat", "current-save-modified");

        var backupsBefore = service.GetSnapshots(target).Count;
        var safetyBefore = snapshotService.GetSnapshots(target.Id, target.BackupPath, SnapshotKind.Safety).Count;

        await service.RestoreAsync(target, snapshotToRestore!);

        var backupsAfter = service.GetSnapshots(target).Count;
        var safetyAfter = snapshotService.GetSnapshots(target.Id, target.BackupPath, SnapshotKind.Safety).Count;

        // The safety snapshot of the current save goes into the separate safety space, not the
        // normal backup history.
        Assert.Equal(backupsBefore, backupsAfter);
        Assert.True(safetyAfter > safetyBefore);
        Assert.Equal("current-save", File.ReadAllText(Path.Combine(save.Path, "save.dat")));
    }

    [Fact]
    public async Task Restore_WithEmptySave_SkipsSafetyBackup()
    {
        using var save = new TempDirectory();
        using var backup = new TempDirectory();
        using var snapshotSource = new TempDirectory();
        snapshotSource.WriteFile("save.dat", "restored");

        var snapshotService = new SnapshotService();
        var service = new BackupService(snapshotService, new TestLogger());
        var targetId = Guid.NewGuid();
        var snapshot = await snapshotService.CreateSnapshotAsync(targetId, snapshotSource.Path, backup.Path);

        var target = new BackupTarget
        {
            Id = targetId,
            DisplayName = "t",
            SourcePath = save.Path, // empty directory
            BackupPath = backup.Path,
            MaxBackups = 10,
        };

        var before = service.GetSnapshots(target).Count;
        await service.RestoreAsync(target, snapshot);
        var after = service.GetSnapshots(target).Count;

        Assert.Equal(before, after); // no safety backup since current save was empty
        Assert.Equal("restored", File.ReadAllText(Path.Combine(save.Path, "save.dat")));
    }

    [Fact]
    public async Task SafetySnapshots_AreStoredSeparatelyFromBackups()
    {
        using var save = new TempDirectory();
        using var backup = new TempDirectory();
        save.WriteFile("save.dat", "v1");

        var snapshotService = new SnapshotService();
        var service = new BackupService(snapshotService, new TestLogger());
        var target = NewTarget(save.Path, backup.Path, maxBackups: 10);

        var toRestore = await service.BackupAsync(target);
        Assert.NotNull(toRestore);

        save.WriteFile("save.dat", "v2");
        await service.RestoreAsync(target, toRestore!);

        var backups = snapshotService.GetSnapshots(target.Id, target.BackupPath, SnapshotKind.Backup);
        var safety = snapshotService.GetSnapshots(target.Id, target.BackupPath, SnapshotKind.Safety);

        // Exactly the one normal backup; the pre-restore safety copy is in its own space.
        Assert.Single(backups);
        Assert.Single(safety);

        // The safety snapshots live under a subfolder, not alongside the normal backups.
        Assert.All(safety, s => Assert.Contains("_safety", s.Path));
        Assert.All(backups, s => Assert.DoesNotContain("_safety", s.Path));
    }

    [Fact]
    public async Task GetSnapshots_Safety_ReturnsSafetySnapshotsNewestFirst()
    {
        using var save = new TempDirectory();
        using var backup = new TempDirectory();
        save.WriteFile("save.dat", "original");

        var service = new BackupService(new SnapshotService(), new TestLogger());
        var target = NewTarget(save.Path, backup.Path, maxBackups: 10);

        var baseline = await service.BackupAsync(target);
        Assert.NotNull(baseline);

        // Two restores, each producing a safety snapshot of the then-current save.
        save.WriteFile("save.dat", "edit-1");
        await service.RestoreAsync(target, baseline!);
        save.WriteFile("save.dat", "edit-2");
        await service.RestoreAsync(target, baseline!);

        var safety = service.GetSnapshots(target, SnapshotKind.Safety);
        Assert.Equal(2, safety.Count);
        Assert.True(safety[0].CreatedAt >= safety[1].CreatedAt); // newest first
    }

    [Fact]
    public async Task Restore_FromSafetySnapshot_RestoresItsContents()
    {
        using var save = new TempDirectory();
        using var backup = new TempDirectory();
        save.WriteFile("save.dat", "state-A");

        var service = new BackupService(new SnapshotService(), new TestLogger());
        var target = NewTarget(save.Path, backup.Path, maxBackups: 10);

        var backupOfA = await service.BackupAsync(target);
        Assert.NotNull(backupOfA);

        // Change the save to state B, then restore A. This creates a safety snapshot capturing B.
        save.WriteFile("save.dat", "state-B");
        await service.RestoreAsync(target, backupOfA!);
        Assert.Equal("state-A", File.ReadAllText(Path.Combine(save.Path, "save.dat")));

        // Now "undo" by restoring from the safety snapshot, which should bring back state B.
        var safety = service.GetSnapshots(target, SnapshotKind.Safety);
        Assert.Single(safety);
        await service.RestoreAsync(target, safety[0]);

        Assert.Equal("state-B", File.ReadAllText(Path.Combine(save.Path, "save.dat")));
    }

    [Fact]
    public async Task TakeManualSnapshot_StoresSeparatelyFromBackups()
    {
        using var save = new TempDirectory();
        using var backup = new TempDirectory();
        save.WriteFile("save.dat", "v1");

        var snapshotService = new SnapshotService();
        var service = new BackupService(snapshotService, new TestLogger());
        var target = NewTarget(save.Path, backup.Path, maxBackups: 10);

        await service.BackupAsync(target);              // one automatic backup
        var manual = await service.TakeManualSnapshotAsync(target);
        Assert.NotNull(manual);

        var backups = snapshotService.GetSnapshots(target.Id, target.BackupPath, SnapshotKind.Backup);
        var manuals = snapshotService.GetSnapshots(target.Id, target.BackupPath, SnapshotKind.Manual);

        Assert.Single(backups);
        Assert.Single(manuals);
        Assert.All(manuals, s => Assert.Contains("_manual", s.Path));
        Assert.All(backups, s => Assert.DoesNotContain("_manual", s.Path));
    }

    [Fact]
    public async Task TakeManualSnapshot_RestoresItsContents()
    {
        using var save = new TempDirectory();
        using var backup = new TempDirectory();
        save.WriteFile("save.dat", "manual-state");

        var service = new BackupService(new SnapshotService(), new TestLogger());
        var target = NewTarget(save.Path, backup.Path, maxBackups: 10);

        var manual = await service.TakeManualSnapshotAsync(target);
        Assert.NotNull(manual);

        save.WriteFile("save.dat", "changed");
        await service.RestoreAsync(target, manual!);

        Assert.Equal("manual-state", File.ReadAllText(Path.Combine(save.Path, "save.dat")));
    }

    [Fact]
    public async Task ManualSnapshotRetention_NeverExceedsMaxBackups()
    {
        using var save = new TempDirectory();
        using var backup = new TempDirectory();

        var service = new BackupService(new SnapshotService(), new TestLogger());
        var target = NewTarget(save.Path, backup.Path, maxBackups: 2);

        for (var i = 0; i < 5; i++)
        {
            save.WriteFile("save.dat", $"v{i}");
            await service.TakeManualSnapshotAsync(target);
            Assert.True(service.GetSnapshots(target, SnapshotKind.Manual).Count <= 2);
        }
    }

    [Fact]
    public async Task GetStorageUsage_ReportsPerCategoryAndTotal()
    {
        using var save = new TempDirectory();
        using var backup = new TempDirectory();
        save.WriteFile("save.dat", "some content here");

        var service = new BackupService(new SnapshotService(), new TestLogger());
        var target = NewTarget(save.Path, backup.Path, maxBackups: 10);

        await service.BackupAsync(target);
        await service.TakeManualSnapshotAsync(target);

        var usage = service.GetStorageUsage(target);

        Assert.True(usage.BackupBytes > 0);
        Assert.True(usage.ManualBytes > 0);
        Assert.Equal(0, usage.SafetyBytes);
        Assert.Equal(usage.BackupBytes + usage.ManualBytes + usage.SafetyBytes, usage.TotalBytes);
    }

    [Fact]
    public async Task DeleteSnapshotAsync_RemovesTheSnapshot()
    {
        using var save = new TempDirectory();
        using var backup = new TempDirectory();
        save.WriteFile("save.dat", "v1");

        var service = new BackupService(new SnapshotService(), new TestLogger());
        var target = NewTarget(save.Path, backup.Path, maxBackups: 10);

        var snapshot = await service.BackupAsync(target);
        Assert.NotNull(snapshot);
        Assert.Single(service.GetSnapshots(target));

        await service.DeleteSnapshotAsync(target, snapshot!);

        Assert.Empty(service.GetSnapshots(target));
        Assert.False(Directory.Exists(snapshot!.Path));
    }

    [Fact]
    public async Task FailedRestore_IsLoggedAsError()
    {
        using var backup = new TempDirectory();
        var fake = new ControllableSnapshotService { FailRestore = true };
        var logger = new TestLogger();
        var service = new BackupService(fake, logger);
        var target = NewTarget("ignored", backup.Path, maxBackups: 5);

        var snapshot = await service.BackupAsync(target);
        Assert.NotNull(snapshot);

        await Assert.ThrowsAsync<IOException>(() => service.RestoreAsync(target, snapshot!));

        Assert.Contains(logger.Entries, e => e.StartsWith("ERROR") && e.Contains("Restore failed"));
    }

    private static BackupTarget NewTarget(string source, string backup, int maxBackups) => new()
    {
        DisplayName = "Test Game",
        SourcePath = source,
        BackupPath = backup,
        MaxBackups = maxBackups,
    };

    /// <summary>
    /// An in-memory snapshot service that records snapshots in a backing directory and can be made
    /// to fail or block on demand, enabling retention/overlap/failure tests without real file churn.
    /// </summary>
    private sealed class ControllableSnapshotService : ISnapshotService
    {
        private readonly List<(Snapshot Snapshot, SnapshotKind Kind)> _snapshots = [];
        private readonly SemaphoreSlim _enteredGate = new(0);
        private TaskCompletionSource? _blockGate;

        public bool FailNext { get; set; }

        public void BlockNext() => _blockGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitUntilInside() => _enteredGate.WaitAsync();

        public void Release() => _blockGate?.TrySetResult();

        public async Task<Snapshot> CreateSnapshotAsync(Guid targetId, string sourcePath, string backupPath, SnapshotKind kind = SnapshotKind.Backup, CancellationToken cancellationToken = default)
        {
            if (_blockGate is { } gate)
            {
                _enteredGate.Release();
                await gate.Task;
                _blockGate = null;
            }

            if (FailNext)
            {
                FailNext = false;
                throw new IOException("simulated failure");
            }

            var snapshot = new Snapshot
            {
                TargetId = targetId,
                Path = Path.Combine(backupPath, targetId.ToString(), kind.ToString(), Guid.NewGuid().ToString("N")),
                CreatedAt = DateTimeOffset.UtcNow.AddTicks(_snapshots.Count),
                SizeBytes = 0,
            };
            _snapshots.Add((snapshot, kind));
            return snapshot;
        }

        public IReadOnlyList<Snapshot> GetSnapshots(Guid targetId, string backupPath, SnapshotKind kind = SnapshotKind.Backup)
            => _snapshots.Where(s => s.Snapshot.TargetId == targetId && s.Kind == kind)
                .OrderBy(s => s.Snapshot.CreatedAt)
                .Select(s => s.Snapshot)
                .ToList();

        public void DeleteSnapshot(Snapshot snapshot) => _snapshots.RemoveAll(s => s.Snapshot.Path == snapshot.Path);

        public bool FailRestore { get; set; }

        public Task RestoreSnapshotAsync(Snapshot snapshot, string destinationPath, CancellationToken cancellationToken = default)
            => FailRestore ? throw new IOException("simulated restore failure") : Task.CompletedTask;

        public bool DiffersFromSnapshot(string sourcePath, Snapshot snapshot) => true;
    }
}
