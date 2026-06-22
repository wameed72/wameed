using System;
using System.IO;
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

    private AppConfig(string connectionString, string manifestUrl, TimeSpan updateInterval)
    {
        ConnectionString = connectionString;
        ManifestUrl = manifestUrl;
        UpdateInterval = updateInterval;
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

        return new AppConfig(connectionString, manifestUrl, TimeSpan.FromDays(intervalDays));
    }
}
