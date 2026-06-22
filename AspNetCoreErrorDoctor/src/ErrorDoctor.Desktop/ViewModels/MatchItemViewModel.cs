using System.Linq;
using ErrorDoctor.Core.Matching;

namespace ErrorDoctor.Desktop.ViewModels;

/// <summary>
/// Display model for a single matched error in the results list.
/// </summary>
public class MatchItemViewModel
{
    public MatchItemViewModel(MatchResult result)
    {
        Title = result.Entry.Title;
        Category = result.Entry.Category;
        Severity = result.Entry.Severity;
        ErrorCode = result.Entry.ErrorCode;
        Cause = result.Entry.Cause;
        Solution = result.Entry.Solution;
        Source = result.Entry.Source;
        SourceUrl = result.Entry.SourceUrl;
        ConfidencePercent = result.ConfidencePercent;
        MatchedTerms = string.Join("، ", result.MatchedTerms.Take(12));
    }

    public string Title { get; }
    public string Category { get; }
    public string Severity { get; }
    public string? ErrorCode { get; }
    public string Cause { get; }
    public string Solution { get; }
    public string Source { get; }
    public string? SourceUrl { get; }
    public int ConfidencePercent { get; }
    public string MatchedTerms { get; }

    public string Header =>
        string.IsNullOrWhiteSpace(ErrorCode) ? Title : $"[{ErrorCode}] {Title}";

    public string ConfidenceText => $"نسبة التطابق: {ConfidencePercent}%";

    public bool HasSourceUrl => !string.IsNullOrWhiteSpace(SourceUrl);
}
