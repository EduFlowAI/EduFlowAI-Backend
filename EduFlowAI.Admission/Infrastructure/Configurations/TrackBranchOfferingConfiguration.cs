using EduFlowAI.Admission.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlowAI.Admission.Infrastructure.Configurations
{
    public class TrackBranchOfferingConfiguration : IEntityTypeConfiguration<TrackBranchOffering>
    {
        public void Configure(EntityTypeBuilder<TrackBranchOffering> builder)
        {
            builder.ToTable("TrackBranchOfferings", "admissions");
            builder.HasKey(x => x.Id);

            // Capacity must be valid
            builder.ToTable(t => t.HasCheckConstraint("CK_TrackBranchOffering_Capacity", "\"Capacity\" > 0"));

            // Prevent duplicating the exact same track in the same branch for the same cycle
            builder.HasIndex(x => new { x.CycleId, x.TrackId, x.BranchId }).IsUnique();

            // Relationships
            builder.HasOne(x => x.Cycle)
                .WithMany(x => x.Offerings)
                .HasForeignKey(x => x.CycleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Track)
                .WithMany(x => x.BranchOfferings)
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Branch)
                .WithMany(x => x.BranchOfferings)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}