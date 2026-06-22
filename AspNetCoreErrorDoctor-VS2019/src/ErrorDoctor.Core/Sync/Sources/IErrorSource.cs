using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Sync.Sources
{

/// <summary>
/// A provider of ASP.NET Core error entries from a trusted platform
/// (curated set, Stack Overflow, GitHub official repos, docs, ...).
/// </summary>
public interface IErrorSource
{
    /// <summary>Human-readable name of the source (shown in status/log messages).</summary>
    string Name { get; }

    /// <summary>True for sources that require the internet (used for offline detection).</summary>
    bool RequiresNetwork { get; }

    /// <summary>
    /// Collects entries from the source. Online sources may throw on network
    /// failures so the caller can distinguish "offline" from "no new data".
    /// </summary>
    Task<IReadOnlyList<ErrorEntryDto>> CollectAsync(CancellationToken cancellationToken = default);
}
}
