using EduFlowAI.Admission.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlowAI.Admission.Infrastructure.Configurations
{
    public class AdmissionCycleConfiguration : IEntityTypeConfiguration<AdmissionCycle>
    {
        public void Configure(EntityTypeBuilder<AdmissionCycle> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            // Optimistic Concurrency mapping to PostgreSQL xmin
            builder.Property(x => x.RowVersion)
                .IsRowVersion()
                .HasColumnName("xmin");

            // Only allow Draft, Active, Closed
            builder.ToTable(t => t.HasCheckConstraint("CK_AdmissionCycle_Status", "\"Status\" IN ('Draft', 'Active', 'Closed')"));

            // Guarantee exactly one Active cycle at a time globally
            builder.HasIndex(x => x.Status)
                .HasDatabaseName("uq_cycle_active")
                .HasFilter("\"Status\" = 'Active'")
                .IsUnique();

            // Relationship: Program -> Cycles (Restrict delete)
            builder.HasOne(x => x.Program)
                .WithMany(x => x.AdmissionCycles)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}