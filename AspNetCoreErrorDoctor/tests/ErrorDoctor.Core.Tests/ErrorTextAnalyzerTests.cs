using System.Linq;
using ErrorDoctor.Core.Matching;
using Xunit;

namespace ErrorDoctor.Core.Tests;

public class ErrorTextAnalyzerTests
{
    [Fact]
    public void Extracts_http_error_code()
    {
        var result = ErrorTextAnalyzer.Analyze("HTTP Error 500.30 - ASP.NET Core app failed to start");
        Assert.Contains("500.30", result.ErrorCodes);
    }

    [Fact]
    public void Extracts_exception_type()
    {
        var result = ErrorTextAnalyzer.Analyze("System.InvalidOperationException: Unable to resolve service");
        Assert.Contains("invalidoperationexception", result.ExceptionTypes);
    }

    [Fact]
    public void Extracts_compiler_error_code()
    {
        var result = ErrorTextAnalyzer.Analyze("error CS1061: 'Foo' does not contain a definition for 'Bar'");
        Assert.Contains("cs1061", result.ErrorCodes);
    }

    [Fact]
    public void Filters_stopwords_and_short_tokens()
    {
        var result = ErrorTextAnalyzer.Analyze("the error was a null value in the object");
        Assert.DoesNotContain("the", result.Keywords);
        Assert.DoesNotContain("error", result.Keywords);
    }

    [Fact]
    public void Normalize_collapses_whitespace_and_lowercases()
    {
        Assert.Equal("500.30", ErrorTextAnalyzer.Normalize("HTTP 500.30"));
    }
}
