using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorDoctor.Core.Data;
using ErrorDoctor.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace ErrorDoctor.Core.Tests
{

public class SyncServiceTests
{
    private static ErrorDoctorDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ErrorDoctorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ErrorDoctorDbContext(options);
    }

    private sealed class FakeSource : IManifestSource
    {
        private readonly ErrorManifest? _manifest;
        public FakeSource(ErrorManifest? manifest) => _manifest = manifest;
        public Task<ErrorManifest?> TryFetchAsync(CancellationToken ct = default) => Task.FromResult(_manifest);
    }

    private static ErrorManifest Manifest(string version, params ErrorEntryDto[] entries) =>
        new() { Version = version, GeneratedAtUtc = DateTime.UtcNow, Entries = entries.ToList() };

    private static ErrorEntryDto Dto(string id, string title, string solution = "fix it") =>
        new() { ExternalId = id, Title = title, Signature = title, Cause = "cause", Solution = solution };

    [Fact]
    public async Task Offline_source_keeps_local_data_and_reports_offline()
    {
        using var db = NewDb();
        await DatabaseInitializer.InitializeAsync(db);
        var before = await db.ErrorEntries.CountAsync();

        var service = new SyncService(db, new FakeSource(null));
        var result = await service.SyncAsync(force: true);

        Assert.Equal(SyncStatus.Offline, result.Status);
        Assert.Equal(before, await db.ErrorEntries.CountAsync());
    }

    [Fact]
    public async Task Adds_new_entries_from_manifest()
    {
        using var db = NewDb();
        await DatabaseInitializer.InitializeAsync(db);

        var manifest = Manifest("v2", Dto("new-1", "Brand new error"), Dto("new-2", "Another new error"));
        var service = new SyncService(db, new FakeSource(manifest));

        var result = await service.SyncAsync(force: true);

        Assert.Equal(SyncStatus.Success, result.Status);
        Assert.Equal(2, result.Added);
        Assert.True(await db.ErrorEntries.AnyAsync(e => e.ExternalId == "new-1"));
    }

    [Fact]
    public async Task Updates_changed_entry_and_skips_unchanged()
    {
        using var db = NewDb();
        await DatabaseInitializer.InitializeAsync(db);

        var v1 = Manifest("v1", Dto("x-1", "Title", "solution A"));
        await new SyncService(db, new FakeSource(v1)).SyncAsync(force: true);

        var v2 = Manifest("v2", Dto("x-1", "Title", "solution B (changed)"));
        var result = await new SyncService(db, new FakeSource(v2)).SyncAsync(force: true);

        Assert.Equal(1, result.Updated);
        var entry = await db.ErrorEntries.FirstAsync(e => e.ExternalId == "x-1");
        Assert.Equal("solution B (changed)", entry.Solution);
    }

    [Fact]
    public async Task Same_version_is_reported_up_to_date_without_force()
    {
        using var db = NewDb();
        await DatabaseInitializer.InitializeAsync(db);

        var manifest = Manifest("v5", Dto("y-1", "Title"));
        await new SyncService(db, new FakeSource(manifest)).SyncAsync(force: true);

        var result = await new SyncService(db, new FakeSource(manifest)).SyncAsync(force: false);

        Assert.Equal(SyncStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task NeedsSync_true_when_never_synced()
    {
        using var db = NewDb();
        await DatabaseInitializer.InitializeAsync(db);
        var service = new SyncService(db, new FakeSource(null));
        Assert.True(await service.NeedsSyncAsync(TimeSpan.FromDays(1)));
    }
}
}
