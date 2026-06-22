using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Sync;

/// <summary>
/// Downloads the update manifest over HTTP (e.g. a GitHub raw JSON URL).
/// Returns null when offline / unreachable so the app keeps working with the local cache.
/// </summary>
public class HttpManifestSource : IManifestSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _manifestUrl;

    public HttpManifestSource(HttpClient httpClient, string manifestUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _manifestUrl = manifestUrl ?? throw new ArgumentNullException(nameof(manifestUrl));
    }

    public async Task<ErrorManifest?> TryFetchAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(_manifestUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<ErrorManifest>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Offline, timed out, or malformed payload: signal "no update available".
            return null;
        }
    }
}
