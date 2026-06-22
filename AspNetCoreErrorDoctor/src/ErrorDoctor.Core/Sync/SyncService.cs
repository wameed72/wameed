using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorDoctor.Core.Data;
using ErrorDoctor.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorDoctor.Core.Sync;

public enum SyncStatus
{
    Success,
    UpToDate,
    Offline,
    Error,
}

public record SyncResult(SyncStatus Status, int Added, int Updated, int Total, string Message);

/// <summary>
/// Keeps the local SQL Server cache up to date from a remote manifest when the internet is available.
/// Designed to be safe to call on every startup and on a daily/weekly timer.
/// </summary>
public class SyncService
{
    private readonly ErrorDoctorDbContext _db;
    private readonly IManifestSource _source;

    public SyncService(ErrorDoctorDbContext db, IManifestSource source)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Returns true when the last successful sync is older than <paramref name="interval"/>.
    /// </summary>
    public async Task<bool> NeedsSyncAsync(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        var meta = await _db.SyncMetadata.AsNoTracking().FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (meta?.LastSyncUtc is null)
        {
            return true;
        }

        return DateTime.UtcNow - meta.LastSyncUtc.Value >= interval;
    }

    public async Task<SyncResult> SyncAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        var meta = await _db.SyncMetadata.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (meta is null)
        {
            meta = new SyncMetadata();
            _db.SyncMetadata.Add(meta);
        }

        var manifest = await _source.TryFetchAsync(cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            meta.LastStatus = "Offline";
            meta.LastMessage = "No internet connection or update source unreachable. Using local data.";
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new SyncResult(SyncStatus.Offline, 0, 0, await _db.ErrorEntries.CountAsync(cancellationToken).ConfigureAwait(false), meta.LastMessage);
        }

        if (!force && manifest.Version == meta.LastManifestVersion && meta.LastSyncUtc is not null)
        {
            meta.LastSyncUtc = DateTime.UtcNow;
            meta.LastStatus = "UpToDate";
            meta.LastMessage = $"Already on the latest version ({manifest.Version}).";
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new SyncResult(SyncStatus.UpToDate, 0, 0, await _db.ErrorEntries.CountAsync(cancellationToken).ConfigureAwait(false), meta.LastMessage);
        }

        int added = 0, updated = 0;

        try
        {
            var existing = await _db.ErrorEntries.ToDictionaryAsync(e => e.ExternalId, cancellationToken).ConfigureAwait(false);

            foreach (var dto in manifest.Entries)
            {
                if (string.IsNullOrWhiteSpace(dto.ExternalId))
                {
                    continue;
                }

                var hash = ContentHasher.ForDto(dto);

                if (existing.TryGetValue(dto.ExternalId, out var entry))
                {
                    if (entry.ContentHash == hash)
                    {
                        continue;
                    }

                    Apply(dto, entry, hash);
                    entry.UpdatedAtUtc = DateTime.UtcNow;
                    updated++;
                }
                else
                {
                    var created = new ErrorEntry { ExternalId = dto.ExternalId, CreatedAtUtc = DateTime.UtcNow };
                    Apply(dto, created, hash);
                    created.UpdatedAtUtc = created.CreatedAtUtc;
                    _db.ErrorEntries.Add(created);
                    added++;
                }
            }

            var total = await _db.ErrorEntries.CountAsync(cancellationToken).ConfigureAwait(false) + added;

            meta.LastSyncUtc = DateTime.UtcNow;
            meta.LastManifestVersion = manifest.Version;
            meta.LastEntryCount = total;
            meta.LastStatus = "Success";
            meta.LastMessage = $"Updated to version {manifest.Version}: +{added} new, {updated} changed.";

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new SyncResult(SyncStatus.Success, added, updated, total, meta.LastMessage);
        }
        catch (Exception ex)
        {
            meta.LastStatus = "Error";
            meta.LastMessage = ex.Message;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new SyncResult(SyncStatus.Error, added, updated, await _db.ErrorEntries.CountAsync(cancellationToken).ConfigureAwait(false), ex.Message);
        }
    }

    private static void Apply(ErrorEntryDto dto, ErrorEntry entry, string hash)
    {
        entry.ErrorCode = dto.ErrorCode;
        entry.Title = dto.Title;
        entry.Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category;
        entry.Signature = dto.Signature;
        entry.Cause = dto.Cause;
        entry.Solution = dto.Solution;
        entry.Source = string.IsNullOrWhiteSpace(dto.Source) ? "Curated" : dto.Source;
        entry.SourceUrl = dto.SourceUrl;
        entry.Tags = dto.Tags;
        entry.Severity = string.IsNullOrWhiteSpace(dto.Severity) ? "Error" : dto.Severity;
        entry.ContentHash = hash;
    }
}
