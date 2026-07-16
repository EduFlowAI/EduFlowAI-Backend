using EduFlowAI.Admission.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Admission.Infrastructure.Configurations
{
    public class TrackConfiguration : IEntityTypeConfiguration<Track>
    {
        public void Configure(EntityTypeBuilder<Track> builder)
        {
            builder.ToTable("Tracks", "admissions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.IsActive).HasDefaultValue(true);

            // Magic happens here: Map the C# List<string> to PostgreSQL JSONB
            builder.Property(x => x.PrerequisiteTopics)
                .HasColumnType("jsonb")
                .IsRequired();

            // Relationship: Program -> Tracks (Restrict delete)
            builder.HasOne(x => x.Program)
                .WithMany(x => x.Tracks)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
