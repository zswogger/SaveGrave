using SaveGuard.Core.Models;

namespace SaveGuard.Desktop.ViewModels;

/// <summary>Presentation wrapper around a <see cref="Snapshot"/> for the backup-history list.</summary>
public sealed class SnapshotItemViewModel : ViewModelBase
{
    public SnapshotItemViewModel(Snapshot snapshot)
    {
        Snapshot = snapshot;
    }

    public Snapshot Snapshot { get; }

    public string CreatedAtText => Snapshot.CreatedAt.ToLocalTime().ToString("MMMM d, yyyy h:mm tt");

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
