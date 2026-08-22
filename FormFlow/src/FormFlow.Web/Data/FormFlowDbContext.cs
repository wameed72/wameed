using FormFlow.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace FormFlow.Web.Data
{
    public class FormFlowDbContext : DbContext
    {
        public FormFlowDbContext(DbContextOptions<FormFlowDbContext> options)
            : base(options)
        {
        }

        public DbSet<FormTemplate> FormTemplates { get; set; }

        public DbSet<FormStage> FormStages { get; set; }

        public DbSet<FormField> FormFields { get; set; }

        public DbSet<Submission> Submissions { get; set; }

        public DbSet<FieldValue> FieldValues { get; set; }

        public DbSet<SubmissionEvent> SubmissionEvents { get; set; }

        public DbSet<AppUser> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FormTemplate>()
                .HasIndex(t => t.PublicToken)
                .IsUnique();

            modelBuilder.Entity<FormTemplate>()
                .HasMany(t => t.Stages)
                .WithOne(s => s.FormTemplate)
                .HasForeignKey(s => s.FormTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FormStage>()
                .HasMany(s => s.Fields)
                .WithOne(f => f.FormStage)
                .HasForeignKey(f => f.FormStageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Submission>()
                .HasIndex(s => s.TrackingCode)
                .IsUnique();

            modelBuilder.Entity<Submission>()
                .HasMany(s => s.Values)
                .WithOne(v => v.Submission)
                .HasForeignKey(v => v.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Submission>()
                .HasMany(s => s.Events)
                .WithOne(e => e.Submission)
                .HasForeignKey(e => e.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FieldValue>()
                .HasOne(v => v.FormField)
                .WithMany()
                .HasForeignKey(v => v.FormFieldId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FieldValue>()
                .HasIndex(v => new { v.SubmissionId, v.FormFieldId })
                .IsUnique();

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Username)
                .IsUnique();
        }
    }
}
