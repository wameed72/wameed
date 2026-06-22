using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorDoctor.Core.Models;
using ErrorDoctor.Core.Sync;
using Microsoft.EntityFrameworkCore;

namespace ErrorDoctor.Core.Data;

/// <summary>
/// Ensures the SQL Server database exists and is seeded with the curated error set on first run.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(ErrorDoctorDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        if (!await db.SyncMetadata.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            db.SyncMetadata.Add(new SyncMetadata { LastStatus = "Never" });
        }

        if (!await db.ErrorEntries.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            var manifest = SeedData.LoadManifest();
            foreach (var dto in manifest.Entries)
            {
                db.ErrorEntries.Add(new ErrorEntry
                {
                    ExternalId = dto.ExternalId,
                    ErrorCode = dto.ErrorCode,
                    Title = dto.Title,
                    Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category,
                    Signature = dto.Signature,
                    Cause = dto.Cause,
                    Solution = dto.Solution,
                    Source = string.IsNullOrWhiteSpace(dto.Source) ? "Curated" : dto.Source,
                    SourceUrl = dto.SourceUrl,
                    Tags = dto.Tags,
                    Severity = string.IsNullOrWhiteSpace(dto.Severity) ? "Error" : dto.Severity,
                    ContentHash = ContentHasher.ForDto(dto),
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
