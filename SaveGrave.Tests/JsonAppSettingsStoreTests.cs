using SaveGrave.Core.Models;
using SaveGrave.Infrastructure;

namespace SaveGrave.Tests;

public class JsonAppSettingsStoreTests
{
    [Fact]
    public void Load_WhenNoFileExists_ReturnsDefaults()
    {
        using var dir = new TempDirectory();
        var store = new JsonAppSettingsStore(dir.Combine("settings.json"));

        var settings = store.Load();

        Assert.False(settings.LaunchAtStartup);
        Assert.True(settings.CloseToTray);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        using var dir = new TempDirectory();
        var path = dir.Combine("settings.json");
        new JsonAppSettingsStore(path).Save(new AppSettings
        {
            LaunchAtStartup = true,
            CloseToTray = false,
            CloseToTrayNoticeShown = true,
        });

        var loaded = new JsonAppSettingsStore(path).Load();

        Assert.True(loaded.LaunchAtStartup);
        Assert.False(loaded.CloseToTray);
        Assert.True(loaded.CloseToTrayNoticeShown);
    }

    [Fact]
    public void Load_WhenFileCorrupt_ReturnsDefaults()
    {
        using var dir = new TempDirectory();
        var path = dir.Combine("settings.json");
        File.WriteAllText(path, "{ not valid json");

        var settings = new JsonAppSettingsStore(path).Load();

        Assert.NotNull(settings);
        Assert.True(settings.CloseToTray);
    }
}
