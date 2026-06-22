using System.Linq;
using ErrorDoctor.Core.Data;
using ErrorDoctor.Core.Matching;
using ErrorDoctor.Core.Models;
using ErrorDoctor.Core.Sync;
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Tests
{

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
}
}
