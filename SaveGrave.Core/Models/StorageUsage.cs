namespace SaveGrave.Core.Models;

/// <summary>
/// Disk usage for a protected target's snapshots, broken down by category plus a total.
/// </summary>
public sealed record StorageUsage(long BackupBytes, long ManualBytes, long SafetyBytes)
{
    public long TotalBytes => BackupBytes + ManualBytes + SafetyBytes;

    public static StorageUsage Empty { get; } = new(0, 0, 0);
}
