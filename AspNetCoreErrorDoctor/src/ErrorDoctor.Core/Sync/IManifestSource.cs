using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Sync;

/// <summary>
/// Abstraction over where update data comes from, so sync can be tested offline.
/// </summary>
public interface IManifestSource
{
    /// <summary>
    /// Returns the manifest, or null if the source is unreachable (offline).
    /// </summary>
    Task<ErrorManifest?> TryFetchAsync(CancellationToken cancellationToken = default);
}
