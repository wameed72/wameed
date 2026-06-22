using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ErrorDoctor.Core.Sync;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Data
{

/// <summary>
/// Loads the curated seed manifest that ships embedded in the assembly so the app is useful
/// on first run with no internet connection.
/// </summary>
public static class SeedData
{
    private const string ResourceName = "ErrorDoctor.Core.Data.seed-errors.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ErrorManifest LoadManifest()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new FileNotFoundException($"Embedded seed resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<ErrorManifest>(json, JsonOptions)
            ?? new ErrorManifest();
    }

    public static IReadOnlyList<ErrorEntryDto> LoadEntries() => LoadManifest().Entries;
}
}
