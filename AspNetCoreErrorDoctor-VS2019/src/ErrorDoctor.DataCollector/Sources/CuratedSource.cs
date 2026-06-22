using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorDoctor.Core.Data;
using ErrorDoctor.Core.Sync;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace ErrorDoctor.DataCollector.Sources
{

/// <summary>
/// The hand-curated, high-quality base set that ships embedded in the app.
/// </summary>
public class CuratedSource : ISource
{
    public string Name => "Curated";

    public Task<IReadOnlyList<ErrorEntryDto>> CollectAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SeedData.LoadEntries());
    }
}
}
