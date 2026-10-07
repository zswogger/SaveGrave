namespace SaveGrave.Core.Services;

/// <summary>
/// Minimal application logger. Kept deliberately small for the MVP — just enough to record what the
/// backup engine is doing and surface failures for diagnosis.
/// </summary>
public interface IAppLogger
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
