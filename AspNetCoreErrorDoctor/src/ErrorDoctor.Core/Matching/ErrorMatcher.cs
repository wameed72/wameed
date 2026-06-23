using System;
using System.Collections.Generic;
using System.Linq;
using ErrorDoctor.Core.Models;

namespace ErrorDoctor.Core.Matching;

/// <summary>
/// Scores known <see cref="ErrorEntry"/> records against a raw error/stack-trace pasted by the user.
/// Works fully offline against the locally cached set of entries.
/// </summary>
public class ErrorMatcher
{
    private const double ErrorCodeWeight = 12.0;
    private const double ExceptionWeight = 6.0;
    private const double KeywordWeight = 1.0;
    private const double FuzzyKeywordWeight = 0.5;

    public IReadOnlyList<MatchResult> Match(string rawText, IEnumerable<ErrorEntry> entries, int maxResults = 5)
    {
        var query = ErrorTextAnalyzer.Analyze(rawText);
        return Match(query, entries, maxResults);
    }

    public IReadOnlyList<MatchResult> Match(AnalyzedQuery query, IEnumerable<ErrorEntry> entries, int maxResults = 5)
    {
        ArgumentNullException.ThrowIfNull(entries);

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
                else if (FuzzyContains(entryTerms, keyword))
                {
                    score += FuzzyKeywordWeight;
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

    /// <summary>
    /// Approximate match for typos / morphological variants (e.g. plural forms): a token counts as a
    /// fuzzy hit when it is within edit distance 1 of an entry term. Kept deliberately tight so that
    /// semantically distinct words (e.g. "related" vs "unrelated") do not match.
    /// </summary>
    private static bool FuzzyContains(HashSet<string> entryTerms, string keyword)
    {
        if (keyword.Length < 5)
        {
            return false;
        }

        foreach (var term in entryTerms)
        {
            if (term.Length < 5 || Math.Abs(term.Length - keyword.Length) > 1)
            {
                continue;
            }

            if (LevenshteinWithin(keyword, term, 1))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns true when the Levenshtein distance between a and b is at most <paramref name="maxDistance"/>.</summary>
    private static bool LevenshteinWithin(string a, string b, int maxDistance)
    {
        int n = a.Length;
        int m = b.Length;
        if (Math.Abs(n - m) > maxDistance)
        {
            return false;
        }

        var previous = new int[m + 1];
        var current = new int[m + 1];
        for (int j = 0; j <= m; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= n; i++)
        {
            current[0] = i;
            int rowMin = current[0];
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                rowMin = Math.Min(rowMin, current[j]);
            }

            if (rowMin > maxDistance)
            {
                return false;
            }

            (previous, current) = (current, previous);
        }

        return previous[m] <= maxDistance;
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
