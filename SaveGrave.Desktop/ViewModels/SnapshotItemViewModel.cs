using SaveGrave.Core.Models;

namespace SaveGrave.Desktop.ViewModels;

/// <summary>Presentation wrapper around a <see cref="Snapshot"/> for the recovery-point timeline.</summary>
public sealed class SnapshotItemViewModel : ViewModelBase
{
    public SnapshotItemViewModel(Snapshot snapshot, string subtitle, GameDetailsViewModel? owner = null)
    {
        Snapshot = snapshot;
        Subtitle = subtitle;
        Owner = owner;
    }

    /// <summary>
    /// Back-reference to the details view model. Flyout menu content lives in a popup outside the
    /// ItemsControl visual tree, so commands bind through the item rather than an ancestor lookup.
    /// </summary>
    public GameDetailsViewModel? Owner { get; }

    public Snapshot Snapshot { get; }

    /// <summary>e.g. "Automatic backup" or "Created before restore".</summary>
    public string Subtitle { get; }

    /// <summary>Time only, used in the timeline rows (date is shown as a group header).</summary>
    public string TimeText => Snapshot.CreatedAt.ToLocalTime().ToString("h:mm tt");

    public string FullDateText => Snapshot.CreatedAt.ToLocalTime().ToString("MMMM d, yyyy h:mm tt");

    public string SizeText => SaveGrave.Core.ByteSize.Format(Snapshot.SizeBytes);

    public string Path => Snapshot.Path;
}
