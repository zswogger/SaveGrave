using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveGuard.Core.Models;

namespace SaveGuard.Desktop.ViewModels;

/// <summary>
/// Backs the Add Game dialog. Collects the fields needed to create a <see cref="BackupTarget"/>.
/// Folder picking is delegated to the view via <see cref="PickFolderAsync"/>.
/// </summary>
public partial class AddGameViewModel : ViewModelBase
{
    /// <summary>Set by the view: given a title, prompts the user for a folder and returns the path or null.</summary>
    public Func<string, Task<string?>>? PickFolderAsync { get; set; }

    [ObservableProperty]
    public partial string GameName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SaveFolder { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BackupLocation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int BackupsToKeep { get; set; } = 20;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public BackupTarget? Result { get; private set; }

    [RelayCommand]
    private async Task BrowseSaveFolder()
    {
        if (PickFolderAsync is null)
            return;
        var picked = await PickFolderAsync("Select the game's save folder");
        if (!string.IsNullOrEmpty(picked))
            SaveFolder = picked;
    }

    [RelayCommand]
    private async Task BrowseBackupLocation()
    {
        if (PickFolderAsync is null)
            return;
        var picked = await PickFolderAsync("Select where backups should be stored");
        if (!string.IsNullOrEmpty(picked))
            BackupLocation = picked;
    }

    /// <summary>Validates input and populates <see cref="Result"/>. Returns true when the dialog may close.</summary>
    public bool TryBuildTarget()
    {
        if (string.IsNullOrWhiteSpace(GameName))
        {
            ErrorMessage = "Enter a game name.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SaveFolder) || !System.IO.Directory.Exists(SaveFolder))
        {
            ErrorMessage = "Select an existing save folder.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(BackupLocation))
        {
            ErrorMessage = "Select a backup location.";
            return false;
        }

        if (SaveGuard.Core.PathUtilities.Overlaps(SaveFolder, BackupLocation))
        {
            ErrorMessage = "The backup location must be outside the save folder (and vice versa).";
            return false;
        }

        if (BackupsToKeep < 1)
        {
            ErrorMessage = "Keep at least one backup.";
            return false;
        }

        Result = new BackupTarget
        {
            DisplayName = GameName.Trim(),
            SourcePath = SaveFolder,
            BackupPath = BackupLocation,
            MaxBackups = BackupsToKeep,
            IsEnabled = true,
        };
        return true;
    }
}
