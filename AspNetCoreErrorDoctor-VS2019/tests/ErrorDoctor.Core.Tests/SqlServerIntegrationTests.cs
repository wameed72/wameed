using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ErrorDoctor.Core.Data;
using ErrorDoctor.Core.Matching;
using ErrorDoctor.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace ErrorDoctor.Core.Tests
{

/// <summary>
/// Exercises the full stack against a real SQL Server instance.
/// Runs only when the ERRORDOCTOR_SQL connection string environment variable is set, so it is
/// skipped on machines without SQL Server (e.g. CI without a database).
/// </summary>
public class SqlServerIntegrationTests
{
    private static string? ConnectionString => Environment.GetEnvironmentVariable("ERRORDOCTOR_SQL");

    private static bool Enabled => !string.IsNullOrWhiteSpace(ConnectionString);

    private static ErrorDoctorDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ErrorDoctorDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new ErrorDoctorDbContext(options);
    }

    private sealed class FileSource : IManifestSource
    {
        private readonly string _path;
        public FileSource(string path) => _path = path;
        public Task<ErrorManifest?> TryFetchAsync(System.Threading.CancellationToken ct = default)
        {
            var json = File.ReadAllText(_path);
            return Task.FromResult(JsonSerializer.Deserialize<ErrorManifest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
    }

    [SkippableFact]
    public async Task Full_stack_init_seed_match_and_sync()
    {
        Skip.IfNot(Enabled, "Set ERRORDOCTOR_SQL to run SQL Server integration tests.");

        using var db = NewDb();
        await db.Database.EnsureDeletedAsync();

        // 1. Initialise + seed.
        await DatabaseInitializer.InitializeAsync(db);
        var seeded = await db.ErrorEntries.CountAsync();
        Assert.True(seeded >= 30, $"Expected the curated seed to be loaded, found {seeded}.");

        // 2. Match a real error against the seeded data.
        var entries = await db.ErrorEntries.AsNoTracking().ToListAsync();
        var matcher = new ErrorMatcher();
        var results = matcher.Match(
            "System.InvalidOperationException: Unable to resolve service for type 'IUserService' while attempting to activate 'HomeController'.",
            entries);
        Assert.Equal("curated-0004", results[0].Entry.ExternalId);

        // 3. Sync new data from a generated manifest file.
        var manifestPath = Path.Combine(Path.GetTempPath(), $"manifest-{Guid.NewGuid():N}.json");
        var manifest = new ErrorManifest
        {
            Version = "itest-1",
            GeneratedAtUtc = DateTime.UtcNow,
            Entries = new()
            {
                new ErrorEntryDto { ExternalId = "itest-x", Title = "Integration test error", Signature = "integration test error", Cause = "c", Solution = "s" },
            },
        };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        try
        {
            var sync = new SyncService(db, new FileSource(manifestPath));
            var result = await sync.SyncAsync(force: true);
            Assert.Equal(SyncStatus.Success, result.Status);
            Assert.True(await db.ErrorEntries.AnyAsync(e => e.ExternalId == "itest-x"));
        }
        finally
        {
            File.Delete(manifestPath);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
}
