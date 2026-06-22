using ErrorDoctor.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Data
{

public class ErrorDoctorDbContext : DbContext
{
    public ErrorDoctorDbContext(DbContextOptions<ErrorDoctorDbContext> options)
        : base(options)
    {
    }

    public DbSet<ErrorEntry> ErrorEntries => Set<ErrorEntry>();

    public DbSet<SyncMetadata> SyncMetadata => Set<SyncMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ErrorEntry>(entity =>
        {
            entity.HasIndex(e => e.ExternalId).IsUnique();
            entity.HasIndex(e => e.ErrorCode);
            entity.HasIndex(e => e.Category);
            entity.Property(e => e.ExternalId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.ErrorCode).HasMaxLength(64);
            entity.Property(e => e.Title).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(128);
            entity.Property(e => e.Source).HasMaxLength(64);
            entity.Property(e => e.SourceUrl).HasMaxLength(1024);
            entity.Property(e => e.Tags).HasMaxLength(1024);
            entity.Property(e => e.Severity).HasMaxLength(32);
            entity.Property(e => e.ContentHash).HasMaxLength(128);
        });

        modelBuilder.Entity<SyncMetadata>(entity =>
        {
            entity.Property(e => e.LastManifestVersion).HasMaxLength(64);
            entity.Property(e => e.LastStatus).HasMaxLength(32);
            entity.Property(e => e.LastMessage).HasMaxLength(2048);
        });
    }
}
}
