using SaveGrave.Core.Models;
using SaveGrave.Infrastructure;

namespace SaveGrave.Tests;

public class FileSystemBackupMonitorTests
{
    [Fact]
    public async Task Debounce_BurstOfChanges_ProducesSingleSnapshot()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();
        source.WriteFile("save.dat", "initial");

        var snapshotService = new SnapshotService();
        var backupService = new BackupService(snapshotService, new TestLogger());
        var target = new BackupTarget
        {
            DisplayName = "Burst Game",
            SourcePath = source.Path,
            BackupPath = backup.Path,
            MaxBackups = 50,
        };

        var completed = 0;
        using var monitor = new FileSystemBackupMonitor(
            backupService,
            snapshotService,
            debounceDelay: TimeSpan.FromMilliseconds(400),
            reconcileInterval: TimeSpan.FromHours(1));
        monitor.BackupCompleted += (_, e) => { if (e.Succeeded) Interlocked.Increment(ref completed); };

        monitor.Start(target);

        // Simulate a save operation: many writes in quick succession, each inside the debounce window.
        for (var i = 0; i < 15; i++)
        {
            source.WriteFile("save.dat", $"write-{i}");
            await Task.Delay(30);
        }

        // Wait well past the debounce window for the single backup to fire and finish.
        await Task.Delay(1500);

        Assert.Equal(1, Volatile.Read(ref completed));
        Assert.Single(backupService.GetSnapshots(target));
    }

    [Fact]
    public async Task RunWithMonitoringPaused_DoesNotBackupFromWritesDuringTheAction()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();
        source.WriteFile("save.dat", "initial");

        var snapshotService = new SnapshotService();
        var backupService = new BackupService(snapshotService, new TestLogger());
        var target = new BackupTarget
        {
            DisplayName = "Paused Game",
            SourcePath = source.Path,
            BackupPath = backup.Path,
            MaxBackups = 50,
        };

        using var monitor = new FileSystemBackupMonitor(
            backupService,
            snapshotService,
            debounceDelay: TimeSpan.FromMilliseconds(300),
            reconcileInterval: TimeSpan.FromHours(1));

        monitor.Start(target);

        await monitor.RunWithMonitoringPausedAsync(target, async () =>
        {
            // Simulate restore writes while monitoring is suspended.
            for (var i = 0; i < 10; i++)
            {
                source.WriteFile("save.dat", $"restore-write-{i}");
                await Task.Delay(20);
            }
        });

        // Give any (incorrectly) scheduled debounce time to fire; it must not, because the writes
        // happened while the watcher was torn down.
        await Task.Delay(700);

        Assert.Empty(backupService.GetSnapshots(target));
    }

    [Fact]
    public async Task Stop_HaltsMonitoring()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();
        source.WriteFile("save.dat", "initial");

        var snapshotService = new SnapshotService();
        var backupService = new BackupService(snapshotService, new TestLogger());
        var target = new BackupTarget
        {
            DisplayName = "Stop Game",
            SourcePath = source.Path,
            BackupPath = backup.Path,
            MaxBackups = 50,
        };

        using var monitor = new FileSystemBackupMonitor(
            backupService,
            snapshotService,
            debounceDelay: TimeSpan.FromMilliseconds(300),
            reconcileInterval: TimeSpan.FromHours(1));

        monitor.Start(target);
        monitor.Stop(target.Id);

        source.WriteFile("save.dat", "changed-after-stop");
        await Task.Delay(800);

        Assert.Empty(backupService.GetSnapshots(target));
    }
}
