using SaveGuard.Core.Models;

namespace SaveGuard.Desktop.ViewModels;

/// <summary>Presentation wrapper around a <see cref="Snapshot"/> for the recovery-point timeline.</summary>
public sealed class SnapshotItemViewModel : ViewModelBase
{
    public SnapshotItemViewModel(Snapshot snapshot, string subtitle)
    {
        Snapshot = snapshot;
        Subtitle = subtitle;
    }

    public Snapshot Snapshot { get; }

    /// <summary>e.g. "Automatic backup" or "Created before restore".</summary>
    public string Subtitle { get; }

    /// <summary>Time only, used in the timeline rows (date is shown as a group header).</summary>
    public string TimeText => Snapshot.CreatedAt.ToLocalTime().ToString("h:mm tt");

    public string FullDateText => Snapshot.CreatedAt.ToLocalTime().ToString("MMMM d, yyyy h:mm tt");

    public string SizeText => FormatSize(Snapshot.SizeBytes);

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[unit]}" : $"{size:0.0} {units[unit]}";
    }
}
