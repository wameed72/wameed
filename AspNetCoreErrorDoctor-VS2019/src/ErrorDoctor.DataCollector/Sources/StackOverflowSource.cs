using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ErrorDoctor.Core.Sync;
using System.IO;

namespace ErrorDoctor.DataCollector.Sources
{

/// <summary>
/// Collects highly-voted, accepted-answer Q&amp;A from Stack Overflow (Stack Exchange API)
/// tagged asp.net-core. No API key required for modest volumes.
/// </summary>
public class StackOverflowSource : ISource
{
    private const string Site = "stackoverflow";

    private readonly HttpClient _http;
    private readonly int _maxQuestions;
    private readonly string _tag;
    private readonly string? _apiKey;

    public StackOverflowSource(HttpClient http, int maxQuestions = 100, string tag = "asp.net-core", string? apiKey = null)
    {
        _http = http;
        _maxQuestions = maxQuestions;
        _tag = tag;
        _apiKey = apiKey;
    }

    public string Name => "StackOverflow";

    public async Task<IReadOnlyList<ErrorEntryDto>> CollectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var questions = await FetchQuestionsAsync(cancellationToken).ConfigureAwait(false);
            var withAccepted = questions.Where(q => q.AcceptedAnswerId is > 0).ToList();
            if (withAccepted.Count == 0)
            {
                return Array.Empty<ErrorEntryDto>();
            }

            var bodies = await FetchAnswerBodiesAsync(
                withAccepted.Select(q => q.AcceptedAnswerId!.Value).Distinct().ToList(),
                cancellationToken).ConfigureAwait(false);

            var results = new List<ErrorEntryDto>();
            foreach (var q in withAccepted)
            {
                if (!bodies.TryGetValue(q.AcceptedAnswerId!.Value, out var body) || string.IsNullOrWhiteSpace(body))
                {
                    continue;
                }

                var title = WebUtility.HtmlDecode(q.Title ?? string.Empty).Trim();
                var solution = HtmlToText(body);
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(solution))
                {
                    continue;
                }

                var tags = q.Tags ?? new List<string>();
                results.Add(new ErrorEntryDto
                {
                    ExternalId = $"so-{q.QuestionId}",
                    ErrorCode = null,
                    Title = Truncate(title, 480),
                    Category = "Community (Stack Overflow)",
                    Signature = title + " " + string.Join(' ', tags),
                    Cause = $"Reported by the community (score {q.Score}). See the linked question for full context.",
                    Solution = Truncate(solution, 6000),
                    Source = "StackOverflow",
                    SourceUrl = q.Link,
                    Tags = string.Join(',', tags),
                    Severity = "Error",
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[StackOverflow] collection failed (continuing without it): {ex.Message}");
            return Array.Empty<ErrorEntryDto>();
        }
    }

    private async Task<List<SoQuestion>> FetchQuestionsAsync(CancellationToken cancellationToken)
    {
        var collected = new List<SoQuestion>();
        int page = 1;

        while (collected.Count < _maxQuestions)
        {
            int pageSize = Math.Min(100, _maxQuestions - collected.Count);
            var url =
                $"https://api.stackexchange.com/2.3/questions?page={page}&pagesize={pageSize}" +
                $"&order=desc&sort=votes&tagged={Uri.EscapeDataString(_tag)}&site={Site}{KeyPart()}";

            var response = await _http.GetFromJsonAsync<SoResponse<SoQuestion>>(url, cancellationToken).ConfigureAwait(false);
            if (response?.Items is null || response.Items.Count == 0)
            {
                break;
            }

            collected.AddRange(response.Items);

            if (!response.HasMore)
            {
                break;
            }

            page++;
            await BackoffAsync(response.Backoff, cancellationToken).ConfigureAwait(false);
        }

        return collected;
    }

    private async Task<Dictionary<long, string>> FetchAnswerBodiesAsync(List<long> answerIds, CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, string>();

        foreach (var batch in Chunk(answerIds, 100))
        {
            var ids = string.Join(';', batch);
            var url =
                $"https://api.stackexchange.com/2.3/answers/{ids}?site={Site}&filter=withbody&pagesize=100{KeyPart()}";

            var response = await _http.GetFromJsonAsync<SoResponse<SoAnswer>>(url, cancellationToken).ConfigureAwait(false);
            if (response?.Items is null)
            {
                continue;
            }

            foreach (var answer in response.Items)
            {
                if (!string.IsNullOrWhiteSpace(answer.Body))
                {
                    map[answer.AnswerId] = answer.Body!;
                }
            }

            await BackoffAsync(response.Backoff, cancellationToken).ConfigureAwait(false);
        }

        return map;
    }

    private string KeyPart() => string.IsNullOrEmpty(_apiKey) ? string.Empty : $"&key={Uri.EscapeDataString(_apiKey)}";

    private static async Task BackoffAsync(int? backoff, CancellationToken cancellationToken)
    {
        if (backoff is > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(backoff.Value), cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        for (int i = 0; i < source.Count; i += size)
        {
            yield return source.Skip(i).Take(size).ToList();
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";

    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);

    private static string HtmlToText(string html)
    {
        var withBreaks = html
            .Replace("</p>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</pre>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</code>", "", StringComparison.OrdinalIgnoreCase);

        var noTags = TagRegex.Replace(withBreaks, string.Empty);
        var decoded = WebUtility.HtmlDecode(noTags);
        var collapsed = Regex.Replace(decoded, @"\n{3,}", "\n\n");
        return collapsed.Trim();
    }

    private sealed class SoResponse<T>
    {
        [JsonPropertyName("items")] public List<T>? Items { get; set; }
        [JsonPropertyName("has_more")] public bool HasMore { get; set; }
        [JsonPropertyName("backoff")] public int? Backoff { get; set; }
    }

    private sealed class SoQuestion
    {
        [JsonPropertyName("question_id")] public long QuestionId { get; set; }
        [JsonPropertyName("accepted_answer_id")] public long? AcceptedAnswerId { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("link")] public string? Link { get; set; }
        [JsonPropertyName("score")] public int Score { get; set; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    }

    private sealed class SoAnswer
    {
        [JsonPropertyName("answer_id")] public long AnswerId { get; set; }
        [JsonPropertyName("score")] public int Score { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
    }
}
}
