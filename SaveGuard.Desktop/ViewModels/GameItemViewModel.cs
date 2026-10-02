using CommunityToolkit.Mvvm.ComponentModel;
using SaveGuard.Core.Models;

namespace SaveGuard.Desktop.ViewModels;

/// <summary>
/// Presentation wrapper around a <see cref="BackupTarget"/> for the protected-games list.
/// </summary>
public partial class GameItemViewModel : ViewModelBase
{
    public GameItemViewModel(BackupTarget target)
    {
        Target = target;
    }

    public BackupTarget Target { get; }

    public Guid Id => Target.Id;

    public string DisplayName => Target.DisplayName;

    [ObservableProperty]
    public partial int BackupCount { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? LastBackupAt { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "Protected";

    public string LastBackupText => LastBackupAt is { } at
        ? $"Last backup: {FormatRelative(at)}"
        : "Last backup: none yet";

    public string BackupCountText => $"Backups: {BackupCount}";

    partial void OnLastBackupAtChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(LastBackupText));

    partial void OnBackupCountChanged(int value) => OnPropertyChanged(nameof(BackupCountText));

    private static string FormatRelative(DateTimeOffset at)
    {
        var delta = DateTimeOffset.UtcNow - at.ToUniversalTime();
        if (delta < TimeSpan.Zero)
            delta = TimeSpan.Zero;

        if (delta.TotalSeconds < 60)
            return "just now";
        if (delta.TotalMinutes < 60)
        {
            var m = (int)delta.TotalMinutes;
            return m == 1 ? "1 minute ago" : $"{m} minutes ago";
        }

        if (delta.TotalHours < 24)
        {
            var h = (int)delta.TotalHours;
            return h == 1 ? "1 hour ago" : $"{h} hours ago";
        }

        var d = (int)delta.TotalDays;
        return d == 1 ? "1 day ago" : $"{d} days ago";
    }
}
