using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorDoctor.Core.Data;

namespace ErrorDoctor.Core.Sync.Sources;

/// <summary>
/// The hand-curated, high-quality base set that ships embedded in the app.
/// </summary>
public class CuratedSource : IErrorSource
{
    public string Name => "Curated";

    public bool RequiresNetwork => false;

    public Task<IReadOnlyList<ErrorEntryDto>> CollectAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SeedData.LoadEntries());
    }
}
