using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ErrorDoctor.Core.Sync;
using ErrorDoctor.Core.Sync.Sources;
using Xunit;

namespace ErrorDoctor.Core.Tests;

public class AggregateManifestSourceTests
{
    private sealed class FakeSource : IErrorSource
    {
        private readonly IReadOnlyList<ErrorEntryDto>? _entries;
        private readonly Exception? _throw;

        public FakeSource(string name, bool requiresNetwork, IReadOnlyList<ErrorEntryDto>? entries = null, Exception? throwOnCollect = null)
        {
            Name = name;
            RequiresNetwork = requiresNetwork;
            _entries = entries;
            _throw = throwOnCollect;
        }

        public string Name { get; }
        public bool RequiresNetwork { get; }

        public Task<IReadOnlyList<ErrorEntryDto>> CollectAsync(CancellationToken cancellationToken = default)
        {
            if (_throw is not null)
            {
                throw _throw;
            }

            return Task.FromResult(_entries ?? Array.Empty<ErrorEntryDto>());
        }
    }

    private static ErrorEntryDto Dto(string id, string title = "title") =>
        new() { ExternalId = id, Title = title, Signature = title, Cause = "c", Solution = "s" };

    [Fact]
    public async Task Returns_null_when_all_network_sources_unreachable()
    {
        var source = new AggregateManifestSource(new IErrorSource[]
        {
            new FakeSource("so", requiresNetwork: true, throwOnCollect: new HttpRequestException("offline")),
            new FakeSource("gh", requiresNetwork: true, throwOnCollect: new TaskCanceledException("timeout")),
        });

        Assert.Null(await source.TryFetchAsync());
    }

    [Fact]
    public async Task Returns_entries_when_at_least_one_network_source_succeeds()
    {
        var source = new AggregateManifestSource(new IErrorSource[]
        {
            new FakeSource("so", requiresNetwork: true, throwOnCollect: new HttpRequestException("offline")),
            new FakeSource("gh", requiresNetwork: true, entries: new[] { Dto("gh-1") }),
        });

        var manifest = await source.TryFetchAsync();

        Assert.NotNull(manifest);
        Assert.Single(manifest!.Entries);
        Assert.Equal("gh-1", manifest.Entries[0].ExternalId);
    }

    [Fact]
    public async Task Local_only_source_never_reports_offline()
    {
        var source = new AggregateManifestSource(new IErrorSource[]
        {
            new FakeSource("curated", requiresNetwork: false, entries: new[] { Dto("c-1") }),
        });

        var manifest = await source.TryFetchAsync();

        Assert.NotNull(manifest);
        Assert.Single(manifest!.Entries);
    }

    [Fact]
    public async Task Later_sources_override_duplicate_ids()
    {
        var source = new AggregateManifestSource(new IErrorSource[]
        {
            new FakeSource("a", requiresNetwork: true, entries: new[] { Dto("dup", "first") }),
            new FakeSource("b", requiresNetwork: true, entries: new[] { Dto("dup", "second") }),
        });

        var manifest = await source.TryFetchAsync();

        Assert.NotNull(manifest);
        Assert.Single(manifest!.Entries);
        Assert.Equal("second", manifest.Entries[0].Title);
    }

    [Fact]
    public async Task Version_is_stable_for_identical_content()
    {
        IErrorSource[] Build() => new IErrorSource[]
        {
            new FakeSource("a", requiresNetwork: true, entries: new[] { Dto("x-1"), Dto("x-2") }),
        };

        var first = await new AggregateManifestSource(Build()).TryFetchAsync();
        var second = await new AggregateManifestSource(Build()).TryFetchAsync();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.Version, second!.Version);
    }
}
