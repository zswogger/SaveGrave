namespace SaveGrave.Core.Models;

/// <summary>
/// A completed, versioned copy of a protected save directory.
/// </summary>
public sealed class Snapshot
{
    public Guid TargetId { get; init; }

    /// <summary>Absolute path to the snapshot directory.</summary>
    public string Path { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public long SizeBytes { get; init; }

    /// <summary>
    /// Relative paths of files that were locked by another process and skipped during this
    /// snapshot (e.g. a running game's open log file). Empty for a fully complete snapshot.
    /// </summary>
    public IReadOnlyList<string> SkippedFiles { get; init; } = [];
}
