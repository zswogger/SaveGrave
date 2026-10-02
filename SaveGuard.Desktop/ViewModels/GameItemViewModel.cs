using CommunityToolkit.Mvvm.ComponentModel;
using SaveGuard.Core.Models;

namespace SaveGuard.Desktop.ViewModels;

/// <summary>Protection state used to drive the status badge presentation.</summary>
public enum ProtectionState
{
    Protected,
    BackingUp,
    Paused,
    Error,
}

/// <summary>
/// Presentation wrapper around a <see cref="BackupTarget"/> for the protected-games library.
/// </summary>
public partial class GameItemViewModel : ViewModelBase
{
    public GameItemViewModel(BackupTarget target)
    {
        Target = target;
    }

    /// <summary>
    /// Back-reference to the owning library view model. Flyout menu content lives in a popup outside
    /// the ItemsControl visual tree, so binding its commands through the item (rather than an ancestor
    /// lookup) is the reliable approach.
    /// </summary>
    public MainViewModel? Owner { get; set; }

    public BackupTarget Target { get; }

    public Guid Id => Target.Id;

    public string DisplayName => Target.DisplayName;

    public string SourcePath => Target.SourcePath;

    public string BackupPath => Target.BackupPath;

    [ObservableProperty]
    public partial int BackupCount { get; set; }

    [ObservableProperty]
    public partial int ManualCount { get; set; }

    [ObservableProperty]
    public partial int SafetyCount { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? LastBackupAt { get; set; }

    /// <summary>Free-form status message (used for error detail). Kept for backward compatibility.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "Protected";

    [ObservableProperty]
    public partial ProtectionState State { get; set; } = ProtectionState.Protected;

    // --- Derived presentation ---

    public string StatusLabel => State switch
    {
        ProtectionState.Protected => "Protected",
        ProtectionState.BackingUp => "Backing up",
        ProtectionState.Paused => "Paused",
        ProtectionState.Error => "Error",
        _ => "Protected",
    };

    // Boolean state flags for class-based styling of the status dot and badge.
    public bool IsProtected => State == ProtectionState.Protected;

    public bool IsBackingUp => State == ProtectionState.BackingUp;

    public bool IsPaused => State == ProtectionState.Paused;

    public bool IsError => State == ProtectionState.Error;

    /// <summary>Live monitoring pulses only while actively protecting.</summary>
    public bool IsLive => State is ProtectionState.Protected or ProtectionState.BackingUp;

    public string PauseResumeLabel => State == ProtectionState.Paused ? "Resume protection" : "Pause protection";

    public string LastBackupValue => LastBackupAt is { } at ? FormatRelative(at) : "No backups yet";

    /// <summary>Re-raises the relative-time text so "x minutes ago" keeps counting up over time.</summary>
    public void RefreshRelativeTimes() => OnPropertyChanged(nameof(LastBackupValue));

    public string BackupCountValue => BackupCount.ToString();

    public string ManualCountValue => ManualCount.ToString();

    public string SafetyCountValue => SafetyCount.ToString();

    partial void OnLastBackupAtChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(LastBackupValue));

    partial void OnBackupCountChanged(int value) => OnPropertyChanged(nameof(BackupCountValue));

    partial void OnManualCountChanged(int value) => OnPropertyChanged(nameof(ManualCountValue));

    partial void OnSafetyCountChanged(int value) => OnPropertyChanged(nameof(SafetyCountValue));

    partial void OnStateChanged(ProtectionState value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(IsProtected));
        OnPropertyChanged(nameof(IsBackingUp));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(IsLive));
        OnPropertyChanged(nameof(PauseResumeLabel));
    }

    private static string FormatRelative(DateTimeOffset at)
    {
        var delta = DateTimeOffset.UtcNow - at.ToUniversalTime();
        if (delta < TimeSpan.Zero)
            delta = TimeSpan.Zero;

        if (delta.TotalSeconds < 60)
            return "Just now";
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
