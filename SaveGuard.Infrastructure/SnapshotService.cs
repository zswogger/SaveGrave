using SaveGuard.Core.Models;
using SaveGuard.Core.Services;

namespace SaveGuard.Infrastructure;

/// <summary>
/// Filesystem implementation of <see cref="ISnapshotService"/>. Snapshots are plain directories
/// named with a UTC timestamp. A snapshot directory only exists once every file has been copied;
/// incomplete copies are written to a temporary directory and discarded on failure.
/// </summary>
public sealed class SnapshotService : ISnapshotService
{
    private const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss";
    private const string IncompleteSuffix = ".incomplete";

    private const string SafetyFolderName = "_safety";

    public async Task<Snapshot> CreateSnapshotAsync(Guid targetId, string sourcePath, string backupPath, SnapshotKind kind = SnapshotKind.Backup, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException($"Save directory not found: {sourcePath}");

        var storageRoot = GetStorageRoot(targetId, backupPath, kind);
        Directory.CreateDirectory(storageRoot);

        var createdAt = DateTimeOffset.UtcNow;
        var finalPath = ReserveSnapshotPath(storageRoot, createdAt);
        var stagingPath = finalPath + IncompleteSuffix;

        if (Directory.Exists(stagingPath))
            Directory.Delete(stagingPath, recursive: true);

        try
        {
            // Guard against a misconfigured backup location that lives inside the save directory:
            // never descend into the backup location, or a snapshot would copy its own output
            // and recurse without end.
            await CopyDirectoryAsync(sourcePath, stagingPath, backupPath, cancellationToken).ConfigureAwait(false);

            // The copy fully succeeded; promote the staging directory to its final name. Only now
            // does the snapshot become visible and therefore "successful".
            Directory.Move(stagingPath, finalPath);
        }
        catch
        {
            TryDeleteDirectory(stagingPath);
            throw;
        }

        return new Snapshot
        {
            TargetId = targetId,
            Path = finalPath,
            CreatedAt = createdAt,
            SizeBytes = GetDirectorySize(finalPath),
        };
    }

    public IReadOnlyList<Snapshot> GetSnapshots(Guid targetId, string backupPath, SnapshotKind kind = SnapshotKind.Backup)
    {
        var storageRoot = GetStorageRoot(targetId, backupPath, kind);
        if (!Directory.Exists(storageRoot))
            return [];

        var snapshots = new List<Snapshot>();
        foreach (var dir in Directory.EnumerateDirectories(storageRoot))
        {
            var name = Path.GetFileName(dir);
            if (name.EndsWith(IncompleteSuffix, StringComparison.Ordinal))
                continue;

            if (!TryParseTimestamp(name, out var createdAt))
                continue;

            snapshots.Add(new Snapshot
            {
                TargetId = targetId,
                Path = dir,
                CreatedAt = createdAt,
                SizeBytes = GetDirectorySize(dir),
            });
        }

        snapshots.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return snapshots;
    }

    public void DeleteSnapshot(Snapshot snapshot)
    {
        if (Directory.Exists(snapshot.Path))
            Directory.Delete(snapshot.Path, recursive: true);
    }

    public async Task RestoreSnapshotAsync(Snapshot snapshot, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(snapshot.Path))
            throw new DirectoryNotFoundException($"Snapshot directory not found: {snapshot.Path}");

        // Stage the restored contents in a sibling temp directory first so a failure midway does
        // not leave the save directory half-overwritten.
        var parent = Directory.GetParent(destinationPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.FullName
                     ?? Path.GetTempPath();
        var staging = Path.Combine(parent, $".saveguard-restore-{Guid.NewGuid():N}");

        try
        {
            await CopyDirectoryAsync(snapshot.Path, staging, excludeRoot: null, cancellationToken).ConfigureAwait(false);

            // Replace the destination's *contents* in place rather than deleting and recreating the
            // directory itself. Removing the save directory would invalidate open handles (e.g. a
            // File Explorer window viewing it) and disrupt any watcher still bound to it.
            Directory.CreateDirectory(destinationPath);
            ClearDirectoryContents(destinationPath);
            await CopyDirectoryAsync(staging, destinationPath, excludeRoot: null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    private static void ClearDirectoryContents(string path)
    {
        foreach (var dir in Directory.EnumerateDirectories(path))
            Directory.Delete(dir, recursive: true);
        foreach (var file in Directory.EnumerateFiles(path))
            File.Delete(file);
    }

    public bool DiffersFromSnapshot(string sourcePath, Snapshot snapshot)
    {
        if (!Directory.Exists(sourcePath))
            return Directory.Exists(snapshot.Path);
        if (!Directory.Exists(snapshot.Path))
            return true;

        var sourceFiles = GetRelativeFileMap(sourcePath);
        var snapshotFiles = GetRelativeFileMap(snapshot.Path);

        if (sourceFiles.Count != snapshotFiles.Count)
            return true;

        foreach (var (relativePath, sourceInfo) in sourceFiles)
        {
            if (!snapshotFiles.TryGetValue(relativePath, out var snapshotInfo))
                return true;
            if (sourceInfo.Length != snapshotInfo.Length)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Picks a snapshot directory name for the given instant. If a directory with that
    /// second-granularity name already exists, appends a counter so bursts never collide.
    /// </summary>
    private static string GetStorageRoot(Guid targetId, string backupPath, SnapshotKind kind)
    {
        var targetRoot = Path.Combine(backupPath, targetId.ToString());
        return kind == SnapshotKind.Safety ? Path.Combine(targetRoot, SafetyFolderName) : targetRoot;
    }

    private static string ReserveSnapshotPath(string targetRoot, DateTimeOffset createdAt)
    {
        var baseName = createdAt.UtcDateTime.ToString(TimestampFormat);
        var candidate = Path.Combine(targetRoot, baseName);
        var counter = 1;
        while (Directory.Exists(candidate) || Directory.Exists(candidate + IncompleteSuffix))
        {
            candidate = Path.Combine(targetRoot, $"{baseName}_{counter}");
            counter++;
        }

        return candidate;
    }

    private static bool TryParseTimestamp(string name, out DateTimeOffset createdAt)
    {
        // Names may carry a collision counter suffix (e.g. "..._1"); the timestamp itself has a
        // fixed length, so parse the leading portion.
        var baseName = name.Length > TimestampFormat.Length ? name[..TimestampFormat.Length] : name;

        if (DateTime.TryParseExact(baseName, TimestampFormat, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            createdAt = new DateTimeOffset(parsed, TimeSpan.Zero);
            return true;
        }

        createdAt = default;
        return false;
    }

    private static async Task CopyDirectoryAsync(string sourceDir, string destinationDir, string? excludeRoot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsExcluded(dir, excludeRoot))
                continue;
            var relative = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(destinationDir, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsExcluded(file, excludeRoot))
                continue;
            var relative = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            await CopyFileAsync(file, destFile, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsExcluded(string path, string? excludeRoot)
        => excludeRoot is not null && SaveGuard.Core.PathUtilities.IsSameOrInside(path, excludeRoot);

    private static async Task CopyFileAsync(string sourceFile, string destFile, CancellationToken cancellationToken)
    {
        const int bufferSize = 81920;
        await using var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        await using var destination = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
        await source.CopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, FileInfo> GetRelativeFileMap(string root)
    {
        var map = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            map[relative] = new FileInfo(file);
        }

        return map;
    }

    private static long GetDirectorySize(string path)
    {
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                total += new FileInfo(file).Length;
            }
            catch (FileNotFoundException)
            {
                // File vanished between enumeration and sizing; ignore.
            }
        }

        return total;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup of incomplete data; surface nothing here.
        }
    }
}
