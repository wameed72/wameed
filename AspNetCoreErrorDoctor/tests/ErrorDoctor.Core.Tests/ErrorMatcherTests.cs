using System.Linq;
using ErrorDoctor.Core.Data;
using ErrorDoctor.Core.Matching;
using ErrorDoctor.Core.Models;
using ErrorDoctor.Core.Sync;
using Xunit;

namespace ErrorDoctor.Core.Tests;

public class ErrorMatcherTests
{
    private static ErrorEntry[] SeedEntries() =>
        SeedData.LoadEntries().Select(dto => new ErrorEntry
        {
            ExternalId = dto.ExternalId,
            ErrorCode = dto.ErrorCode,
            Title = dto.Title,
            Category = dto.Category,
            Signature = dto.Signature,
            Cause = dto.Cause,
            Solution = dto.Solution,
            Tags = dto.Tags,
        }).ToArray();

    [Fact]
    public void Matches_di_resolution_error_as_top_result()
    {
        var matcher = new ErrorMatcher();
        var text = "System.InvalidOperationException: Unable to resolve service for type 'MyApp.IUserService' while attempting to activate 'MyApp.Controllers.HomeController'.";

        var results = matcher.Match(text, SeedEntries());

        Assert.NotEmpty(results);
        Assert.Equal("curated-0004", results[0].Entry.ExternalId);
    }

    [Fact]
    public void Matches_http_500_30_by_error_code()
    {
        var matcher = new ErrorMatcher();
        var results = matcher.Match("HTTP Error 500.30 ANCM In-Process Start Failure", SeedEntries());

        Assert.Equal("curated-0001", results[0].Entry.ExternalId);
        Assert.True(results[0].ConfidencePercent > 0);
    }

    [Fact]
    public void Matches_cors_error()
    {
        var matcher = new ErrorMatcher();
        var text = "Access to fetch has been blocked by CORS policy: No 'Access-Control-Allow-Origin' header is present on the requested resource.";

        var results = matcher.Match(text, SeedEntries());

        Assert.Equal("curated-0013", results[0].Entry.ExternalId);
    }

    [Fact]
    public void Returns_empty_when_nothing_matches()
    {
        var matcher = new ErrorMatcher();
        var results = matcher.Match("zzzqqq totally unrelated gibberish foobarbaz", SeedEntries());
        Assert.Empty(results);
    }

    [Fact]
    public void Respects_max_results()
    {
        var matcher = new ErrorMatcher();
        var results = matcher.Match("ef core dbcontext sql server connection migration entity tracking", SeedEntries(), maxResults: 3);
        Assert.True(results.Count <= 3);
    }

    private static ErrorEntry[] CustomEntry() => new[]
    {
        new ErrorEntry
        {
            ExternalId = "x-1",
            Title = "Dependency injection provider could not resolve service",
            Category = "DI",
            Signature = "InvalidOperationException unable resolve service provider middleware",
            Cause = "c",
            Solution = "s",
            Tags = "di,provider",
        },
    };

    [Fact]
    public void Fuzzy_matches_single_character_typo()
    {
        var matcher = new ErrorMatcher();

        // "provder" is edit distance 1 from "provider"; exact matching would miss it.
        var results = matcher.Match("provder middleware", CustomEntry());

        Assert.NotEmpty(results);
        Assert.Equal("x-1", results[0].Entry.ExternalId);
    }

    [Fact]
    public void Fuzzy_matches_substring_keyword()
    {
        var matcher = new ErrorMatcher();

        // "middlewares" contains the entry term "middleware".
        var results = matcher.Match("middlewares pipeline", CustomEntry());

        Assert.NotEmpty(results);
        Assert.Equal("x-1", results[0].Entry.ExternalId);
    }
}
