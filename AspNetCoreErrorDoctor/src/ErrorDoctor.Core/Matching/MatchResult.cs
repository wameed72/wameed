using System.Collections.Generic;
using ErrorDoctor.Core.Models;

namespace ErrorDoctor.Core.Matching;

public class MatchResult
{
    public required ErrorEntry Entry { get; init; }

    /// <summary>
    /// Raw weighted score produced by the matcher.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Score normalised to 0-100 relative to the best possible signals in the query.
    /// </summary>
    public int ConfidencePercent { get; init; }

    public IReadOnlyList<string> MatchedTerms { get; init; } = new List<string>();
}
