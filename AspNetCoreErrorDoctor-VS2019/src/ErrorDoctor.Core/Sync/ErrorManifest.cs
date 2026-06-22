using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Sync
{

/// <summary>
/// Shape of the JSON document downloaded during an update. Hosted on a trusted source
/// (e.g. a GitHub raw URL) and produced by the data collector.
/// </summary>
public class ErrorManifest
{
    public string Version { get; set; } = "0";

    public DateTime GeneratedAtUtc { get; set; }

    public List<ErrorEntryDto> Entries { get; set; } = new();
}

public class ErrorEntryDto
{
    public string ExternalId { get; set; } = string.Empty;

    public string? ErrorCode { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = "General";

    public string Signature { get; set; } = string.Empty;

    public string Cause { get; set; } = string.Empty;

    public string Solution { get; set; } = string.Empty;

    public string Source { get; set; } = "Curated";

    public string? SourceUrl { get; set; }

    public string Tags { get; set; } = string.Empty;

    public string Severity { get; set; } = "Error";
}
}
