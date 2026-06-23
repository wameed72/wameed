using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ErrorDoctor.Core.Sync.Sources;

namespace ErrorDoctor.Core.Sync
{

/// <summary>
/// Builds an update manifest on the fly by querying several trusted platforms
/// (Stack Overflow, official .NET GitHub repos, ...) and merging their entries.
/// This lets the desktop app update directly from the internet when the user
/// presses "Update", without needing a separately-hosted manifest file.
///
/// Returns <c>null</c> only when every network-backed source is unreachable
/// (i.e. the machine is offline), so the caller keeps using the local cache.
/// </summary>
public class AggregateManifestSource : IManifestSource
{
    private readonly IReadOnlyList<IErrorSource> _sources;

    public AggregateManifestSource(IReadOnlyList<IErrorSource> sources)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    /// <summary>
    /// Convenience factory for the set of trusted online sources used by the app's Update button.
    /// </summary>
    public static AggregateManifestSource CreateDefault(
        HttpClient http,
        int maxStackOverflow = 100,
        string stackOverflowTag = "asp.net-core",
        string? stackAppsKey = null,
        IReadOnlyList<string>? gitHubRepos = null,
        string? gitHubToken = null)
    {
        var sources = new List<IErrorSource>
        {
            new StackOverflowSource(http, maxStackOverflow, stackOverflowTag, stackAppsKey),
            new GitHubIssuesSource(http, gitHubRepos, token: gitHubToken),
            new MicrosoftLearnSource(http),
        };

        if (!string.IsNullOrWhiteSpace(gitHubToken))
        {
            sources.Add(new GitHubDiscussionsSource(http, gitHubRepos, token: gitHubToken));
        }

        return new AggregateManifestSource(sources);
    }

    public async Task<ErrorManifest?> TryFetchAsync(CancellationToken cancellationToken = default)
    {
        var merged = new Dictionary<string, ErrorEntryDto>(StringComparer.OrdinalIgnoreCase);

        int networkSources = 0;
        int networkSucceeded = 0;

        foreach (var source in _sources)
        {
            if (source.RequiresNetwork)
            {
                networkSources++;
            }

            try
            {
                var entries = await source.CollectAsync(cancellationToken).ConfigureAwait(false);
                if (source.RequiresNetwork)
                {
                    networkSucceeded++;
                }

                foreach (var dto in entries)
                {
                    if (!string.IsNullOrWhiteSpace(dto.ExternalId))
                    {
                        merged[dto.ExternalId] = dto;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or OperationCanceledException)
            {
                // This source is unreachable; try the rest and let offline detection below decide.
            }
        }

        // Offline: there were network sources but not a single one could be reached.
        if (networkSources > 0 && networkSucceeded == 0)
        {
            return null;
        }

        var entriesList = merged.Values
            .OrderBy(e => e.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ErrorManifest
        {
            Version = ComputeVersion(entriesList),
            GeneratedAtUtc = DateTime.UtcNow,
            Entries = entriesList,
        };
    }

    /// <summary>
    /// Content-derived version so an unchanged remote produces the same version
    /// (lets non-forced syncs report "up to date" instead of re-applying).
    /// </summary>
    private static string ComputeVersion(IReadOnlyList<ErrorEntryDto> entries)
    {
        if (entries.Count == 0)
        {
            return "empty";
        }

        var parts = entries.Select(e => e.ExternalId + ":" + ContentHasher.ForDto(e)).ToArray();
        return ContentHasher.Compute(parts)[..16];
    }
}
}
