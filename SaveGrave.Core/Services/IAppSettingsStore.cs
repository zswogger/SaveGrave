using SaveGrave.Core.Models;

namespace SaveGrave.Core.Services;

/// <summary>Loads and saves application-wide settings.</summary>
public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
