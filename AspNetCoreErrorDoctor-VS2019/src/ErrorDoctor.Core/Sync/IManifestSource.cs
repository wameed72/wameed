using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace ErrorDoctor.Core.Sync
{

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
}
