using System;

namespace ErrorDoctor.Core.Models;

/// <summary>
/// A single known ASP.NET Core error together with its diagnosis and solution.
/// </summary>
public class ErrorEntry
{
    public int Id { get; set; }

    /// <summary>
    /// Stable identifier used to upsert the entry during sync (e.g. "curated-0001", "so-7565423").
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>
    /// Optional ASP.NET Core / HTTP / runtime error code (e.g. "HTTP 500.30", "CS1061").
    /// </summary>
    public string? ErrorCode { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = "General";

    /// <summary>
    /// Space separated key terms / phrases used by the matcher (exception type names, key words).
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    public string Cause { get; set; } = string.Empty;

    public string Solution { get; set; } = string.Empty;

    /// <summary>
    /// Origin of the entry: Curated, StackOverflow, MicrosoftLearn, GitHub.
    /// </summary>
    public string Source { get; set; } = "Curated";

    public string? SourceUrl { get; set; }

    /// <summary>
    /// Comma separated tags.
    /// </summary>
    public string Tags { get; set; } = string.Empty;

    /// <summary>
    /// Info, Warning, Error, Critical.
    /// </summary>
    public string Severity { get; set; } = "Error";

    /// <summary>
    /// Hash of the meaningful content; used to detect changes during sync.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
