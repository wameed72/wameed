using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Sync.Sources;

/// <summary>
/// Collects resolved, highly-reacted issues from official .NET GitHub repositories
/// (e.g. dotnet/aspnetcore, dotnet/runtime) via the public GitHub Search API.
/// Network failures propagate so callers can detect the offline state.
/// </summary>
public class GitHubIssuesSource : IErrorSource
{
    private static readonly string[] DefaultRepos = { "dotnet/aspnetcore", "dotnet/runtime" };

    private readonly HttpClient _http;
    private readonly IReadOnlyList<string> _repos;
    private readonly int _maxPerRepo;
    private readonly string? _token;

    public GitHubIssuesSource(HttpClient http, IReadOnlyList<string>? repos = null, int maxPerRepo = 40, string? token = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _repos = repos is { Count: > 0 } ? repos : DefaultRepos;
        _maxPerRepo = maxPerRepo;
        _token = token;
    }

    public string Name => "GitHub (official .NET repos)";

    public bool RequiresNetwork => true;

    public async Task<IReadOnlyList<ErrorEntryDto>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ErrorEntryDto>();

        foreach (var repo in _repos)
        {
            int perPage = Math.Min(100, _maxPerRepo);
            var query = $"repo:{repo} is:issue is:closed label:bug sort:reactions-+1-desc";
            var url =
                $"https://api.github.com/search/issues?q={Uri.EscapeDataString(query)}&per_page={perPage}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.UserAgent.ParseAdd("AspNetCoreErrorDoctor/1.0");
            if (!string.IsNullOrEmpty(_token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            }

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Rate-limited or repo unavailable: skip this repo but keep others.
                continue;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GitHubSearchResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (payload?.Items is null)
            {
                continue;
            }

            foreach (var issue in payload.Items)
            {
                if (string.IsNullOrWhiteSpace(issue.Title) || string.IsNullOrWhiteSpace(issue.HtmlUrl))
                {
                    continue;
                }

                var labels = issue.Labels?.Select(l => l.Name ?? string.Empty)
                    .Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? new List<string>();
                var body = string.IsNullOrWhiteSpace(issue.Body)
                    ? "See the linked GitHub issue for the full discussion and resolution."
                    : issue.Body!.Trim();

                results.Add(new ErrorEntryDto
                {
                    ExternalId = $"gh-{repo.Replace('/', '-')}-{issue.Number}",
                    ErrorCode = null,
                    Title = Truncate(issue.Title!.Trim(), 480),
                    Category = $"Official ({repo})",
                    Signature = issue.Title!.Trim() + " " + string.Join(' ', labels),
                    Cause = $"Tracked and resolved in {repo} (issue #{issue.Number}, {issue.Reactions?.TotalCount ?? 0} reactions).",
                    Solution = Truncate(body, 6000),
                    Source = "GitHub",
                    SourceUrl = issue.HtmlUrl,
                    Tags = string.Join(',', labels),
                    Severity = "Error",
                });
            }
        }

        return results;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";

    private sealed class GitHubSearchResponse
    {
        [JsonPropertyName("items")] public List<GitHubIssue>? Items { get; set; }
    }

    private sealed class GitHubIssue
    {
        [JsonPropertyName("number")] public long Number { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("labels")] public List<GitHubLabel>? Labels { get; set; }
        [JsonPropertyName("reactions")] public GitHubReactions? Reactions { get; set; }
    }

    private sealed class GitHubLabel
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private sealed class GitHubReactions
    {
        [JsonPropertyName("total_count")] public int TotalCount { get; set; }
    }
}
