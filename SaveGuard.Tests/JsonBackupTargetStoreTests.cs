using SaveGuard.Core.Models;
using SaveGuard.Infrastructure;

namespace SaveGuard.Tests;

public class JsonBackupTargetStoreTests
{
    [Fact]
    public async Task SaveThenLoad_RoundTripsTargets()
    {
        using var dir = new TempDirectory();
        var configPath = dir.Combine("targets.json");
        var store = new JsonBackupTargetStore(configPath);

        var targets = new List<BackupTarget>
        {
            new()
            {
                DisplayName = "RuneScape: Dragonwilds",
                SourcePath = @"C:\saves\rs",
                BackupPath = @"D:\backups\rs",
                MaxBackups = 7,
                IsEnabled = true,
            },
            new()
            {
                DisplayName = "Another Game",
                SourcePath = "/home/user/saves/ag",
                BackupPath = "/home/user/backups/ag",
                MaxBackups = 15,
                IsEnabled = false,
            },
        };

        await store.SaveAsync(targets);
        var loaded = await store.LoadAsync();

        Assert.Equal(2, loaded.Count);

        var first = loaded.Single(t => t.Id == targets[0].Id);
        Assert.Equal("RuneScape: Dragonwilds", first.DisplayName);
        Assert.Equal(@"C:\saves\rs", first.SourcePath);
        Assert.Equal(@"D:\backups\rs", first.BackupPath);
        Assert.Equal(7, first.MaxBackups);
        Assert.True(first.IsEnabled);

        var second = loaded.Single(t => t.Id == targets[1].Id);
        Assert.Equal(15, second.MaxBackups);
        Assert.False(second.IsEnabled);
    }

    [Fact]
    public async Task Load_WhenNoFileExists_ReturnsEmpty()
    {
        using var dir = new TempDirectory();
        var store = new JsonBackupTargetStore(dir.Combine("missing.json"));

        var loaded = await store.LoadAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task Save_PersistsAcrossNewStoreInstances()
    {
        using var dir = new TempDirectory();
        var configPath = dir.Combine("sub", "targets.json");

        var target = new BackupTarget { DisplayName = "Persisted", SourcePath = "s", BackupPath = "b", MaxBackups = 3 };
        await new JsonBackupTargetStore(configPath).SaveAsync([target]);

        var loaded = await new JsonBackupTargetStore(configPath).LoadAsync();

        Assert.Single(loaded);
        Assert.Equal("Persisted", loaded[0].DisplayName);
    }
}
