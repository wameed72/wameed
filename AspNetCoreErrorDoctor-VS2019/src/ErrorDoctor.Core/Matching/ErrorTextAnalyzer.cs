using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Matching
{

/// <summary>
/// Extracts meaningful signals (error codes, exception types, keywords) from raw error text.
/// </summary>
public static class ErrorTextAnalyzer
{
    private static readonly Regex ErrorCodeRegex = new(
        @"HTTP\s+(?:Error\s+)?\d{3}(?:\.\d+)?|\b\d{3}\.\d{1,3}\b|\b(?:CS\d{3,5}|MSB\d{3,5}|NETSDK\d{3,5}|SQL\d{3,5}|0x[0-9A-Fa-f]{6,8})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExceptionRegex = new(
        @"\b[A-Z][A-Za-z0-9]*Exception\b",
        RegexOptions.Compiled);

    private static readonly Regex TokenRegex = new(
        @"[A-Za-z][A-Za-z0-9\.]+",
        RegexOptions.Compiled);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "this", "that", "from", "into", "your", "you", "was", "were",
        "has", "have", "had", "are", "but", "not", "all", "any", "can", "could", "would", "should",
        "while", "when", "then", "than", "there", "their", "they", "what", "which", "will", "been",
        "system", "microsoft", "net", "exception", "error", "errors", "value", "object", "type",
        "line", "file", "code", "stack", "trace", "inner", "message", "info", "warn", "fail", "failed",
        "occurred", "during", "request", "method", "class", "void", "string", "null", "true", "false",
    };

    public static AnalyzedQuery Analyze(string rawText)
    {
        rawText ??= string.Empty;

        var errorCodes = ErrorCodeRegex.Matches(rawText)
            .Select(m => Normalize(m.Value))
            .Distinct()
            .ToList();

        var exceptions = ExceptionRegex.Matches(rawText)
            .Select(m => m.Value.ToLowerInvariant())
            .Distinct()
            .ToList();

        var keywords = TokenRegex.Matches(rawText)
            .Select(m => m.Value.ToLowerInvariant().Trim('.'))
            .Where(t => t.Length >= 3 && !StopWords.Contains(t) && !IsNumeric(t))
            .Distinct()
            .ToList();

        return new AnalyzedQuery(errorCodes, exceptions, keywords);
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Canonicalise error codes so "HTTP 500.30", "HTTP Error 500.30" and "500.30" all match:
        // lowercase, drop the http/error words and any whitespace.
        var lowered = value.ToLowerInvariant();
        lowered = Regex.Replace(lowered, @"\b(?:http|error)\b", string.Empty);
        return Regex.Replace(lowered, @"\s+", string.Empty);
    }

    private static bool IsNumeric(string token) => token.All(c => char.IsDigit(c) || c == '.');
}

public record AnalyzedQuery(
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> ExceptionTypes,
    IReadOnlyList<string> Keywords);
}
