using SaveGrave.Core.Models;

namespace SaveGrave.Core.Services;

/// <summary>
/// Persists the set of configured backup targets so they survive application restarts.
/// </summary>
public interface IBackupTargetStore
{
    Task<IReadOnlyList<BackupTarget>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IEnumerable<BackupTarget> targets, CancellationToken cancellationToken = default);
}
