namespace SaveGuard.Core.Models;

/// <summary>
/// A game save directory that Save Grave protects by creating versioned backups.
/// </summary>
public sealed class BackupTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The active save directory to protect. Save Grave only writes here during a restore.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>The root directory under which snapshots for this target are stored.</summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>Maximum number of successful snapshots to retain. Older snapshots beyond this are pruned.</summary>
    public int MaxBackups { get; set; } = 20;

    public bool IsEnabled { get; set; } = true;
}
