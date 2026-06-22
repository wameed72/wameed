using System;
using System.Collections.Generic;
using System.Linq;
using ErrorDoctor.Core.Models;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Matching
{

/// <summary>
/// Scores known <see cref="ErrorEntry"/> records against a raw error/stack-trace pasted by the user.
/// Works fully offline against the locally cached set of entries.
/// </summary>
public class ErrorMatcher
{
    private const double ErrorCodeWeight = 12.0;
    private const double ExceptionWeight = 6.0;
    private const double KeywordWeight = 1.0;

    public IReadOnlyList<MatchResult> Match(string rawText, IEnumerable<ErrorEntry> entries, int maxResults = 5)
    {
        var query = ErrorTextAnalyzer.Analyze(rawText);
        return Match(query, entries, maxResults);
    }

    public IReadOnlyList<MatchResult> Match(AnalyzedQuery query, IEnumerable<ErrorEntry> entries, int maxResults = 5)
    {
        if (entries is null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        double bestPossible =
            (query.ErrorCodes.Count > 0 ? ErrorCodeWeight : 0) +
            (query.ExceptionTypes.Count > 0 ? ExceptionWeight : 0) +
            Math.Min(query.Keywords.Count, 8) * KeywordWeight;
        if (bestPossible <= 0)
        {
            bestPossible = 1;
        }

        var results = new List<MatchResult>();

        foreach (var entry in entries)
        {
            var entryTerms = BuildEntryTerms(entry);
            var entryCode = ErrorTextAnalyzer.Normalize(entry.ErrorCode ?? string.Empty);

            double score = 0;
            var matched = new List<string>();

            foreach (var code in query.ErrorCodes)
            {
                if (!string.IsNullOrEmpty(entryCode) && entryCode == code)
                {
                    score += ErrorCodeWeight;
                    matched.Add(code);
                }
                else if (entryTerms.Contains(code))
                {
                    score += ErrorCodeWeight * 0.5;
                    matched.Add(code);
                }
            }

            foreach (var ex in query.ExceptionTypes)
            {
                if (entryTerms.Contains(ex))
                {
                    score += ExceptionWeight;
                    matched.Add(ex);
                }
            }

            foreach (var keyword in query.Keywords)
            {
                if (entryTerms.Contains(keyword))
                {
                    score += KeywordWeight;
                    matched.Add(keyword);
                }
            }

            if (score <= 0)
            {
                continue;
            }

            int confidence = (int)Math.Round(Math.Min(100.0, score / bestPossible * 100.0));

            results.Add(new MatchResult
            {
                Entry = entry,
                Score = score,
                ConfidencePercent = confidence,
                MatchedTerms = matched.Distinct().ToList(),
            });
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.ConfidencePercent)
            .Take(maxResults)
            .ToList();
    }

    private static HashSet<string> BuildEntryTerms(ErrorEntry entry)
    {
        var text = string.Join(' ', new[]
        {
            entry.Signature,
            entry.Title,
            entry.Tags?.Replace(',', ' '),
            entry.Category,
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var analyzed = ErrorTextAnalyzer.Analyze(text);
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var k in analyzed.Keywords) terms.Add(k);
        foreach (var ex in analyzed.ExceptionTypes) terms.Add(ex);

        // Signature tokens may include exception type names that the keyword filter strips,
        // so add the raw lowercased signature words too.
        foreach (var word in (entry.Signature ?? string.Empty).Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            terms.Add(word.ToLowerInvariant().Trim('.'));
        }

        var code = ErrorTextAnalyzer.Normalize(entry.ErrorCode ?? string.Empty);
        if (!string.IsNullOrEmpty(code))
        {
            terms.Add(code);
        }

        return terms;
    }
}
}
