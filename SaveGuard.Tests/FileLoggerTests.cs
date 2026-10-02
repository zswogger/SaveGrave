using SaveGuard.Infrastructure;

namespace SaveGuard.Tests;

public class FileLoggerTests
{
    [Fact]
    public void Error_WritesEntryWithExceptionToLogsDirectory()
    {
        using var dir = new TempDirectory();
        var logsDir = dir.Combine("logs");
        var logger = new FileLogger(logsDir);

        logger.Info("starting up");
        logger.Error("restore failed", new InvalidOperationException("boom"));

        var logFiles = Directory.GetFiles(logsDir, "*.log");
        Assert.Single(logFiles);

        var contents = File.ReadAllText(logFiles[0]);
        Assert.Contains("[INFO] starting up", contents);
        Assert.Contains("[ERROR] restore failed", contents);
        Assert.Contains("boom", contents);
    }

    [Fact]
    public void Logger_DoesNotThrow_WhenDirectoryCannotBeCreated()
    {
        // An invalid path must not surface as an application failure.
        var logger = new FileLogger("\0invalid\0path");
        var ex = Record.Exception(() => logger.Info("should be swallowed"));
        Assert.Null(ex);
    }
}
