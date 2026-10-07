using SaveGrave.Core;

namespace SaveGrave.Tests;

public class PathUtilitiesTests
{
    [Fact]
    public void IsSameOrInside_SamePath_IsTrue()
    {
        using var dir = new TempDirectory();
        Assert.True(PathUtilities.IsSameOrInside(dir.Path, dir.Path));
    }

    [Fact]
    public void IsSameOrInside_NestedChild_IsTrue()
    {
        using var dir = new TempDirectory();
        var child = dir.Combine("backups", "inner");
        Assert.True(PathUtilities.IsSameOrInside(child, dir.Path));
    }

    [Fact]
    public void IsSameOrInside_Sibling_IsFalse()
    {
        using var a = new TempDirectory();
        using var b = new TempDirectory();
        Assert.False(PathUtilities.IsSameOrInside(a.Path, b.Path));
    }

    [Fact]
    public void IsSameOrInside_SimilarPrefixButNotNested_IsFalse()
    {
        // "C:\games" must not be considered inside "C:\game".
        var child = Path.Combine(Path.GetTempPath(), "game-saves");
        var parent = Path.Combine(Path.GetTempPath(), "game");
        Assert.False(PathUtilities.IsSameOrInside(child, parent));
    }

    [Fact]
    public void Overlaps_DetectsEitherDirection()
    {
        using var save = new TempDirectory();
        var backupInsideSave = save.Combine("backups");

        Assert.True(PathUtilities.Overlaps(save.Path, backupInsideSave));
        Assert.True(PathUtilities.Overlaps(backupInsideSave, save.Path));
    }
}
