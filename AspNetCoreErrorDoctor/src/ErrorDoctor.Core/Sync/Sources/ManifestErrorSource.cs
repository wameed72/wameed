using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Sync.Sources;

/// <summary>
/// Adapts a hosted manifest (<see cref="IManifestSource"/>) into an <see cref="IErrorSource"/>
/// so a custom/self-hosted manifest can be aggregated alongside the live platform sources.
/// </summary>
public class ManifestErrorSource : IErrorSource
{
    private readonly IManifestSource _manifestSource;

    public ManifestErrorSource(IManifestSource manifestSource, string? name = null)
    {
        _manifestSource = manifestSource ?? throw new ArgumentNullException(nameof(manifestSource));
        Name = name ?? "Custom manifest";
    }

    public string Name { get; }

    public bool RequiresNetwork => true;

    public async Task<IReadOnlyList<ErrorEntryDto>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await _manifestSource.TryFetchAsync(cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            // Unreachable: surface as a network failure so the aggregator counts it correctly.
            throw new System.Net.Http.HttpRequestException($"Manifest source '{Name}' is unreachable.");
        }

        return manifest.Entries;
    }
}
