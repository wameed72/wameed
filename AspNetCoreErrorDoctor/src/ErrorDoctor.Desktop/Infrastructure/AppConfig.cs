using System;
using Microsoft.Extensions.Configuration;

namespace ErrorDoctor.Desktop.Infrastructure;

/// <summary>
/// Strongly typed access to appsettings.json (connection string + update settings).
/// </summary>
public class AppConfig
{
    public string ConnectionString { get; }

    public string ManifestUrl { get; }

    public TimeSpan UpdateInterval { get; }

    public bool EnableStackOverflow { get; }

    public bool EnableGitHub { get; }

    public bool EnableMicrosoftLearn { get; }

    public bool EnableGitHubDiscussions { get; }

    public string StackOverflowTag { get; }

    public int MaxStackOverflowQuestions { get; }

    public string? StackAppsKey { get; }

    public string? GitHubToken { get; }

    /// <summary>True when at least one update source (live platform or hosted manifest) is configured.</summary>
    public bool HasAnyUpdateSource =>
        EnableStackOverflow || EnableGitHub || EnableMicrosoftLearn || EnableGitHubDiscussions
        || !string.IsNullOrWhiteSpace(ManifestUrl);

    private AppConfig(
        string connectionString,
        string manifestUrl,
        TimeSpan updateInterval,
        bool enableStackOverflow,
        bool enableGitHub,
        bool enableMicrosoftLearn,
        bool enableGitHubDiscussions,
        string stackOverflowTag,
        int maxStackOverflowQuestions,
        string? stackAppsKey,
        string? gitHubToken)
    {
        ConnectionString = connectionString;
        ManifestUrl = manifestUrl;
        UpdateInterval = updateInterval;
        EnableStackOverflow = enableStackOverflow;
        EnableGitHub = enableGitHub;
        EnableMicrosoftLearn = enableMicrosoftLearn;
        EnableGitHubDiscussions = enableGitHubDiscussions;
        StackOverflowTag = stackOverflowTag;
        MaxStackOverflowQuestions = maxStackOverflowQuestions;
        StackAppsKey = stackAppsKey;
        GitHubToken = gitHubToken;
    }

    public static AppConfig Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var connectionString = configuration.GetConnectionString("ErrorDoctor")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=ErrorDoctor;Trusted_Connection=True;TrustServerCertificate=True";

        var manifestUrl = configuration["Update:ManifestUrl"] ?? string.Empty;

        var intervalDays = 1.0;
        if (double.TryParse(configuration["Update:IntervalDays"], out var parsed) && parsed > 0)
        {
            intervalDays = parsed;
        }

        var enableStackOverflow = ParseBool(configuration["Update:Sources:StackOverflow"], defaultValue: true);
        var enableGitHub = ParseBool(configuration["Update:Sources:GitHub"], defaultValue: true);
        var enableMicrosoftLearn = ParseBool(configuration["Update:Sources:MicrosoftLearn"], defaultValue: true);
        var enableGitHubDiscussions = ParseBool(configuration["Update:Sources:GitHubDiscussions"], defaultValue: false);
        var tag = configuration["Update:Sources:Tag"];
        if (string.IsNullOrWhiteSpace(tag))
        {
            tag = "asp.net-core";
        }

        var maxQuestions = 100;
        if (int.TryParse(configuration["Update:Sources:MaxStackOverflowQuestions"], out var parsedMax) && parsedMax > 0)
        {
            maxQuestions = parsedMax;
        }

        var stackAppsKey = NullIfEmpty(configuration["Update:Sources:StackAppsKey"]);
        var gitHubToken = NullIfEmpty(configuration["Update:Sources:GitHubToken"]);

        return new AppConfig(
            connectionString,
            manifestUrl,
            TimeSpan.FromDays(intervalDays),
            enableStackOverflow,
            enableGitHub,
            enableMicrosoftLearn,
            enableGitHubDiscussions,
            tag,
            maxQuestions,
            stackAppsKey,
            gitHubToken);
    }

    private static bool ParseBool(string? value, bool defaultValue) =>
        bool.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
