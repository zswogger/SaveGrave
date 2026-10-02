namespace SaveGuard.Core;

/// <summary>
/// Helpers for reasoning about the relationship between two filesystem paths. Used to prevent a
/// backup location from overlapping the save directory, which would cause a snapshot to copy its
/// own output and recurse endlessly.
/// </summary>
public static class PathUtilities
{
    /// <summary>
    /// Returns true if <paramref name="childPath"/> is the same as, or nested within,
    /// <paramref name="parentPath"/>.
    /// </summary>
    public static bool IsSameOrInside(string childPath, string parentPath)
    {
        var child = Normalize(childPath);
        var parent = Normalize(parentPath);

        if (string.Equals(child, parent, PathComparison))
            return true;

        var parentWithSeparator = parent + Path.DirectorySeparatorChar;
        return child.StartsWith(parentWithSeparator, PathComparison);
    }

    /// <summary>
    /// Returns true if the two paths overlap in either direction (same, or one contained in the
    /// other). Such a pair is unsafe to use as save folder and backup location.
    /// </summary>
    public static bool Overlaps(string pathA, string pathB)
        => IsSameOrInside(pathA, pathB) || IsSameOrInside(pathB, pathA);

    private static string Normalize(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    // Windows and macOS paths are case-insensitive; Linux is case-sensitive. Default to the OS
    // convention via the platform's path comparison type.
    private static StringComparison PathComparison =>
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}
