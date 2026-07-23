using EduFlowAI.Admission.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlowAI.Admission.Infrastructure.Configurations
{
    public class AdmissionCycleConfiguration : IEntityTypeConfiguration<AdmissionCycle>
    {
        public void Configure(EntityTypeBuilder<AdmissionCycle> builder)
        {
            builder.ToTable("AdmissionCycles", "admissions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Label)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.RowVersion)
                .IsRowVersion()
                .HasColumnName("xmin");

            builder.ToTable(t => t.HasCheckConstraint(
                "CK_AdmissionCycle_Status",
                "\"Status\" IN ('Draft', 'Active', 'Closed')"));

            builder.Property(x => x.StartDate)
                .HasColumnType("date")
                .IsRequired();

            builder.Property(x => x.DeadlineUtc)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .HasDefaultValueSql("now()")
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .HasDefaultValueSql("now()")
                .IsRequired();

            builder.Property(x => x.ClosedAt)
                .HasColumnType("timestamp with time zone");

            builder.HasIndex(x => new { x.ProgramId, x.Label })
                .HasDatabaseName("UX_AdmissionCycles_Program_Label")
                .IsUnique();

            // PostgreSQL partial unique index: at most one Active cycle globally.
            builder.HasIndex(x => x.Status)
                .HasDatabaseName("uq_cycle_active")
                .HasFilter("\"Status\" = 'Active'")
                .IsUnique();

            builder.HasOne(x => x.Program)
                .WithMany(x => x.AdmissionCycles)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
