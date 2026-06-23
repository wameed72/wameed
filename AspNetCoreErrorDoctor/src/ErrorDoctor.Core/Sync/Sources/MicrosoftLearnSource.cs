using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Sync.Sources;

/// <summary>
/// Collects official documentation from Microsoft Learn via its public search API.
/// Each result becomes a reference entry (title + description + link).
/// Network failures propagate so callers can detect the offline state.
/// </summary>
public class MicrosoftLearnSource : IErrorSource
{
    private static readonly string[] DefaultQueries =
    {
        "asp.net core exception",
        "asp.net core error",
        "entity framework core exception",
    };

    private readonly HttpClient _http;
    private readonly IReadOnlyList<string> _queries;
    private readonly int _maxPerQuery;
    private readonly string _locale;

    public MicrosoftLearnSource(HttpClient http, IReadOnlyList<string>? queries = null, int maxPerQuery = 25, string locale = "en-us")
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _queries = queries is { Count: > 0 } ? queries : DefaultQueries;
        _maxPerQuery = maxPerQuery;
        _locale = string.IsNullOrWhiteSpace(locale) ? "en-us" : locale;
    }

    public string Name => "Microsoft Learn (docs)";

    public bool RequiresNetwork => true;

    public async Task<IReadOnlyList<ErrorEntryDto>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ErrorEntryDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in _queries)
        {
            int top = Math.Min(50, _maxPerQuery);
            var url =
                $"https://learn.microsoft.com/api/search?search={Uri.EscapeDataString(query)}" +
                $"&locale={Uri.EscapeDataString(_locale)}&$top={top}";

            var response = await _http.GetFromJsonAsync<LearnResponse>(url, cancellationToken).ConfigureAwait(false);
            if (response?.Results is null)
            {
                continue;
            }

            foreach (var item in response.Results)
            {
                if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Url))
                {
                    continue;
                }

                if (!seen.Add(item.Url))
                {
                    continue;
                }

                var description = WebUtility.HtmlDecode(item.Description ?? string.Empty).Trim();
                var title = WebUtility.HtmlDecode(item.Title).Trim();
                var solution = string.IsNullOrWhiteSpace(description)
                    ? "See the official Microsoft Learn documentation for details."
                    : description;

                results.Add(new ErrorEntryDto
                {
                    ExternalId = $"learn-{ContentHasher.Compute(item.Url)[..16]}",
                    ErrorCode = null,
                    Title = Truncate(title, 480),
                    Category = "Docs (Microsoft Learn)",
                    Signature = title,
                    Cause = "Official Microsoft Learn documentation.",
                    Solution = Truncate(solution, 6000),
                    Source = "MicrosoftLearn",
                    SourceUrl = item.Url,
                    Tags = "docs,microsoft-learn",
                    Severity = "Info",
                });
            }
        }

        return results;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";

    private sealed class LearnResponse
    {
        [JsonPropertyName("results")] public List<LearnResult>? Results { get; set; }
    }

    private sealed class LearnResult
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
    }
}
