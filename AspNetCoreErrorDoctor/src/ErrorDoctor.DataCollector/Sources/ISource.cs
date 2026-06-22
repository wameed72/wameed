using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorDoctor.Core.Sync;

namespace ErrorDoctor.DataCollector.Sources;

/// <summary>
/// A provider of ASP.NET Core error entries (curated set, Stack Overflow, GitHub issues, docs, ...).
/// </summary>
public interface ISource
{
    string Name { get; }

    Task<IReadOnlyList<ErrorEntryDto>> CollectAsync(CancellationToken cancellationToken = default);
}
