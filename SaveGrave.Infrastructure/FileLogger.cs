using SaveGrave.Core;
using SaveGrave.Core.Services;

namespace SaveGrave.Infrastructure;

/// <summary>
/// Appends log entries to a daily file under the per-user logs directory. Writes are serialized so
/// entries from background backups and the UI thread do not interleave or race.
/// </summary>
public sealed class FileLogger : IAppLogger
{
    private readonly string _logsDirectory;
    private readonly object _sync = new();

    public FileLogger(string? logsDirectory = null)
    {
        _logsDirectory = logsDirectory ?? AppPaths.LogsDirectory;
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}";
        if (exception is not null)
            line += Environment.NewLine + exception;

        lock (_sync)
        {
            try
            {
                Directory.CreateDirectory(_logsDirectory);
                var file = Path.Combine(_logsDirectory, $"SaveGrave-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(file, line + Environment.NewLine);
            }
            catch
            {
                // Logging must never throw into the application. If the log can't be written there
                // is nothing more we can safely do here.
            }
        }
    }
}
