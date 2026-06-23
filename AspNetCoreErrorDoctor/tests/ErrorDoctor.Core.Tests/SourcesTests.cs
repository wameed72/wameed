using System.Net.Http;
using System.Threading.Tasks;
using ErrorDoctor.Core.Sync.Sources;
using Xunit;

namespace ErrorDoctor.Core.Tests;

public class SourcesTests
{
    [Fact]
    public async Task GitHubDiscussions_without_token_yields_nothing_and_makes_no_request()
    {
        // No token => GraphQL cannot be queried; must return empty without touching the network.
        using var http = new HttpClient();
        var source = new GitHubDiscussionsSource(http, token: null);

        var entries = await source.CollectAsync();

        Assert.Empty(entries);
    }

    [Fact]
    public void Sources_declare_network_requirement()
    {
        using var http = new HttpClient();
        Assert.True(new MicrosoftLearnSource(http).RequiresNetwork);
        Assert.True(new GitHubDiscussionsSource(http).RequiresNetwork);
        Assert.False(new CuratedSource().RequiresNetwork);
    }
}
