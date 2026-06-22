using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ErrorDoctor.Core.Sync;
using ErrorDoctor.DataCollector.Sources;

namespace ErrorDoctor.DataCollector;

/// <summary>
/// Builds the update manifest (error-manifest.json) by merging the curated base set with
/// data aggregated from trusted public sources. Host the output (e.g. on a GitHub raw URL)
/// and point the desktop app's update source at it.
///
/// Usage:
///   dotnet run --project src/ErrorDoctor.DataCollector -- \
///       --output dist/error-manifest.json [--stackoverflow] [--max 200] [--tag asp.net-core]
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = CommandLineOptions.Parse(args);

        var sources = new List<ISource> { new CuratedSource() };

        using var http = CreateHttpClient();
        if (options.IncludeStackOverflow)
        {
            sources.Add(new StackOverflowSource(http, options.MaxQuestions, options.Tag, options.StackAppsKey));
        }

        var merged = new Dictionary<string, ErrorEntryDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            Console.WriteLine($"Collecting from {source.Name}...");
            var entries = await source.CollectAsync().ConfigureAwait(false);
            int added = 0;
            foreach (var dto in entries)
            {
                if (string.IsNullOrWhiteSpace(dto.ExternalId))
                {
                    continue;
                }

                merged[dto.ExternalId] = dto; // later sources can override duplicates by id
                added++;
            }

            Console.WriteLine($"  {source.Name}: {added} entries.");
        }

        var manifest = new ErrorManifest
        {
            Version = DateTime.UtcNow.ToString("yyyyMMdd.HHmmss"),
            GeneratedAtUtc = DateTime.UtcNow,
            Entries = merged.Values.OrderBy(e => e.ExternalId, StringComparer.OrdinalIgnoreCase).ToList(),
        };

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        });

        var outputPath = Path.GetFullPath(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, json).ConfigureAwait(false);

        Console.WriteLine($"Wrote {manifest.Entries.Count} entries (version {manifest.Version}) to {outputPath}");
        return 0;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AspNetCoreErrorDoctor-DataCollector/1.0");
        return http;
    }
}

internal sealed class CommandLineOptions
{
    public string OutputPath { get; private set; } = "dist/error-manifest.json";
    public bool IncludeStackOverflow { get; private set; }
    public int MaxQuestions { get; private set; } = 100;
    public string Tag { get; private set; } = "asp.net-core";
    public string? StackAppsKey { get; private set; }

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--output" or "-o" when i + 1 < args.Length:
                    options.OutputPath = args[++i];
                    break;
                case "--stackoverflow" or "--so":
                    options.IncludeStackOverflow = true;
                    break;
                case "--max" when i + 1 < args.Length && int.TryParse(args[i + 1], out var max):
                    options.MaxQuestions = max;
                    i++;
                    break;
                case "--tag" when i + 1 < args.Length:
                    options.Tag = args[++i];
                    break;
                case "--key" when i + 1 < args.Length:
                    options.StackAppsKey = args[++i];
                    break;
            }
        }

        return options;
    }
}
