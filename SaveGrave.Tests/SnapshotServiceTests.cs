using SaveGrave.Infrastructure;

namespace SaveGrave.Tests;

public class SnapshotServiceTests
{
    [Fact]
    public async Task CreateSnapshot_CopiesAllFilesAndNestedDirectories()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();
        source.WriteFile("save.dat", "root");
        source.WriteFile(Path.Combine("profiles", "player1.json"), "p1");
        source.WriteFile(Path.Combine("profiles", "sub", "deep.txt"), "deep");

        var service = new SnapshotService();
        var targetId = Guid.NewGuid();

        var snapshot = await service.CreateSnapshotAsync(targetId, source.Path, backup.Path);

        Assert.True(File.Exists(Path.Combine(snapshot.Path, "save.dat")));
        Assert.True(File.Exists(Path.Combine(snapshot.Path, "profiles", "player1.json")));
        Assert.True(File.Exists(Path.Combine(snapshot.Path, "profiles", "sub", "deep.txt")));
    }

    [Fact]
    public async Task CreateSnapshot_PreservesFileContents()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();
        source.WriteFile("save.dat", "the quick brown fox");
        source.WriteFile(Path.Combine("nested", "data.bin"), "0123456789");

        var service = new SnapshotService();
        var snapshot = await service.CreateSnapshotAsync(Guid.NewGuid(), source.Path, backup.Path);

        Assert.Equal("the quick brown fox", File.ReadAllText(Path.Combine(snapshot.Path, "save.dat")));
        Assert.Equal("0123456789", File.ReadAllText(Path.Combine(snapshot.Path, "nested", "data.bin")));
    }

    [Fact]
    public async Task CreateSnapshot_FailsWhenSourceMissing_WritesNothing()
    {
        using var backup = new TempDirectory();
        var service = new SnapshotService();
        var targetId = Guid.NewGuid();
        var missingSource = Path.Combine(backup.Path, "does-not-exist");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => service.CreateSnapshotAsync(targetId, missingSource, backup.Path));

        Assert.Empty(service.GetSnapshots(targetId, backup.Path));
    }

    [Fact]
    public async Task Restore_RestoresSnapshotContents()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();
        using var restoreTarget = new TempDirectory();
        source.WriteFile("save.dat", "original");
        source.WriteFile(Path.Combine("deep", "nested.txt"), "nested-value");

        var service = new SnapshotService();
        var snapshot = await service.CreateSnapshotAsync(Guid.NewGuid(), source.Path, backup.Path);

        await service.RestoreSnapshotAsync(snapshot, restoreTarget.Path);

        Assert.Equal("original", File.ReadAllText(Path.Combine(restoreTarget.Path, "save.dat")));
        Assert.Equal("nested-value", File.ReadAllText(Path.Combine(restoreTarget.Path, "deep", "nested.txt")));
    }

    [Fact]
    public async Task Restore_ReplacesExistingContents()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();
        using var dest = new TempDirectory();
        source.WriteFile("keep.txt", "from-snapshot");

        var service = new SnapshotService();
        var snapshot = await service.CreateSnapshotAsync(Guid.NewGuid(), source.Path, backup.Path);

        // A stale file that must not survive the restore.
        dest.WriteFile("stale.txt", "should-be-gone");

        await service.RestoreSnapshotAsync(snapshot, dest.Path);

        Assert.True(File.Exists(Path.Combine(dest.Path, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(dest.Path, "stale.txt")));
    }

    [Fact]
    public async Task Restore_PreservesTheSaveDirectoryItself_NotJustContents()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();
        using var dest = new TempDirectory();
        source.WriteFile("save.dat", "snapshot-content");

        var service = new SnapshotService();
        var snapshot = await service.CreateSnapshotAsync(Guid.NewGuid(), source.Path, backup.Path);

        // The restore must replace contents in place, not delete and recreate the directory, so an
        // open handle on the folder (e.g. a File Explorer window) stays valid. We approximate
        // "same directory" by its creation timestamp, which would change on delete+recreate.
        var createdBefore = Directory.GetCreationTimeUtc(dest.Path);
        dest.WriteFile("stale.txt", "gone-after-restore");

        await service.RestoreSnapshotAsync(snapshot, dest.Path);

        Assert.Equal(createdBefore, Directory.GetCreationTimeUtc(dest.Path));
        Assert.True(File.Exists(Path.Combine(dest.Path, "save.dat")));
        Assert.False(File.Exists(Path.Combine(dest.Path, "stale.txt")));
    }

    [Fact]
    public async Task DiffersFromSnapshot_DetectsChanges()
    {
        using var source = new TempDirectory();
        using var backup = new TempDirectory();
        source.WriteFile("save.dat", "v1");

        var service = new SnapshotService();
        var snapshot = await service.CreateSnapshotAsync(Guid.NewGuid(), source.Path, backup.Path);

        Assert.False(service.DiffersFromSnapshot(source.Path, snapshot));

        source.WriteFile("save.dat", "v1-with-more-bytes");
        Assert.True(service.DiffersFromSnapshot(source.Path, snapshot));
    }

    [Fact]
    public async Task CreateSnapshot_WithBackupRootInsideSave_DoesNotRecurseIntoItsOwnOutput()
    {
        // Misconfiguration: backup location lives inside the save directory. The snapshot copy must
        // exclude the backup subtree so it terminates and never copies prior snapshots into itself.
        using var save = new TempDirectory();
        save.WriteFile("save.dat", "payload");
        save.WriteFile(Path.Combine("profiles", "p1.json"), "profile");
        var backupPath = save.Combine("SaveGraveBackups");

        var service = new SnapshotService();
        var targetId = Guid.NewGuid();

        var first = await service.CreateSnapshotAsync(targetId, save.Path, backupPath);
        var second = await service.CreateSnapshotAsync(targetId, save.Path, backupPath);

        // Both snapshots contain the real save files...
        Assert.True(File.Exists(Path.Combine(second.Path, "save.dat")));
        Assert.True(File.Exists(Path.Combine(second.Path, "profiles", "p1.json")));

        // ...but neither contains a copy of the backup directory (no self-nesting).
        Assert.False(Directory.Exists(Path.Combine(second.Path, "SaveGraveBackups")));
        Assert.Equal(2, service.GetSnapshots(targetId, backupPath).Count);
        _ = first;
    }
}
