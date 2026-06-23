using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Sync.Sources
{
    /// <summary>
    /// Collects answered discussions from official .NET GitHub repositories via the GraphQL API.
    /// Requires a GitHub token (GraphQL is authenticated-only); without one the source yields nothing.
    /// Network failures propagate so callers can detect the offline state.
    /// </summary>
    public class GitHubDiscussionsSource : IErrorSource
    {
        private const string GraphQlQuery =
            "query($q:String!,$n:Int!){search(query:$q,type:DISCUSSION,first:$n){nodes{... on Discussion{" +
            "number title url body repository{nameWithOwner} answer{body} reactions{totalCount}}}}}";

        private static readonly string[] DefaultRepos = { "dotnet/aspnetcore", "dotnet/runtime" };

        private readonly HttpClient _http;
        private readonly IReadOnlyList<string> _repos;
        private readonly int _maxPerRepo;
        private readonly string? _token;

        public GitHubDiscussionsSource(HttpClient http, IReadOnlyList<string>? repos = null, int maxPerRepo = 25, string? token = null)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _repos = repos is { Count: > 0 } ? repos : DefaultRepos;
            _maxPerRepo = maxPerRepo;
            _token = token;
        }

        public string Name => "GitHub Discussions (official .NET repos)";

        public bool RequiresNetwork => true;

        public async Task<IReadOnlyList<ErrorEntryDto>> CollectAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_token))
            {
                // GraphQL requires authentication; nothing we can do without a token.
                return Array.Empty<ErrorEntryDto>();
            }

            var results = new List<ErrorEntryDto>();

            foreach (var repo in _repos)
            {
                var payload = new
                {
                    query = GraphQlQuery,
                    variables = new { q = $"repo:{repo} is:answered", n = Math.Min(50, _maxPerRepo) },
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql")
                {
                    Content = JsonContent.Create(payload),
                };
                request.Headers.UserAgent.ParseAdd("AspNetCoreErrorDoctor/1.0");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var data = await response.Content
                    .ReadFromJsonAsync<GraphQlResponse>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                var nodes = data?.Data?.Search?.Nodes;
                if (nodes is null)
                {
                    continue;
                }

                foreach (var node in nodes)
                {
                    if (string.IsNullOrWhiteSpace(node.Title) || string.IsNullOrWhiteSpace(node.Url))
                    {
                        continue;
                    }

                    var answer = node.Answer?.Body;
                    var solution = string.IsNullOrWhiteSpace(answer)
                        ? (string.IsNullOrWhiteSpace(node.Body)
                            ? "See the linked GitHub discussion for the accepted answer."
                            : node.Body.Trim())
                        : answer.Trim();

                    results.Add(new ErrorEntryDto
                    {
                        ExternalId = $"ghd-{repo.Replace('/', '-')}-{node.Number}",
                        ErrorCode = null,
                        Title = Truncate(node.Title.Trim(), 480),
                        Category = $"Discussion ({repo})",
                        Signature = node.Title.Trim(),
                        Cause = $"Answered community discussion in {repo} (#{node.Number}, {node.Reactions?.TotalCount ?? 0} reactions).",
                        Solution = Truncate(solution, 6000),
                        Source = "GitHubDiscussions",
                        SourceUrl = node.Url,
                        Tags = "discussion,github",
                        Severity = "Info",
                    });
                }
            }

            return results;
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value.Substring(0, max) + "...";

        private sealed class GraphQlResponse
        {
            [JsonPropertyName("data")] public GraphQlData? Data { get; set; }
        }

        private sealed class GraphQlData
        {
            [JsonPropertyName("search")] public SearchConnection? Search { get; set; }
        }

        private sealed class SearchConnection
        {
            [JsonPropertyName("nodes")] public List<DiscussionNode>? Nodes { get; set; }
        }

        private sealed class DiscussionNode
        {
            [JsonPropertyName("number")] public long Number { get; set; }
            [JsonPropertyName("title")] public string? Title { get; set; }
            [JsonPropertyName("url")] public string? Url { get; set; }
            [JsonPropertyName("body")] public string? Body { get; set; }
            [JsonPropertyName("answer")] public DiscussionAnswer? Answer { get; set; }
            [JsonPropertyName("reactions")] public ReactionConnection? Reactions { get; set; }
        }

        private sealed class DiscussionAnswer
        {
            [JsonPropertyName("body")] public string? Body { get; set; }
        }

        private sealed class ReactionConnection
        {
            [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
        }
    }
}
