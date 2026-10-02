using System.Text.Json;
using SaveGuard.Core.Models;
using SaveGuard.Core.Services;

namespace SaveGuard.Infrastructure;

/// <summary>
/// Persists backup targets as a JSON file under a per-user application data directory so the
/// configuration survives restarts and is not written beside the executable.
/// </summary>
public sealed class JsonBackupTargetStore : IBackupTargetStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _configFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public JsonBackupTargetStore(string? configFilePath = null)
    {
        _configFilePath = configFilePath ?? GetDefaultConfigPath();
    }

    public string ConfigFilePath => _configFilePath;

    public async Task<IReadOnlyList<BackupTarget>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_configFilePath))
                return [];

            await using var stream = new FileStream(_configFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var targets = await JsonSerializer
                .DeserializeAsync<List<BackupTarget>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return targets ?? [];
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAsync(IEnumerable<BackupTarget> targets, CancellationToken cancellationToken = default)
    {
        var list = targets.ToList();

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);

            // Write to a temp file then move into place so a crash mid-write cannot corrupt the
            // existing configuration.
            var tempPath = _configFilePath + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, list, SerializerOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, _configFilePath, overwrite: true);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static string GetDefaultConfigPath() => SaveGuard.Core.AppPaths.ConfigFilePath;
}
