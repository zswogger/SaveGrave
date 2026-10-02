using System.Collections.Concurrent;
using SaveGuard.Core.Services;

namespace SaveGuard.Tests;

/// <summary>Captures log entries in memory so tests can assert on them without touching the disk.</summary>
public sealed class TestLogger : IAppLogger
{
    public ConcurrentQueue<string> Entries { get; } = new();

    public void Info(string message) => Entries.Enqueue($"INFO {message}");

    public void Warn(string message) => Entries.Enqueue($"WARN {message}");

    public void Error(string message, Exception? exception = null)
        => Entries.Enqueue($"ERROR {message} {exception?.Message}");
}
