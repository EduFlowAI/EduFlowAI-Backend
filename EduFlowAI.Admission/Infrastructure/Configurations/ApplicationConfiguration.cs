using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Admission.Infrastructure.Configurations
{
    public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
    {
        public void Configure(EntityTypeBuilder<Application> builder)
        {
            builder.ToTable("Applications", schema: "admissions");

            builder.HasKey(a => a.Id);

            builder.Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                //.HasDefaultValue(ApplicationStatus.Draft)
                .IsRequired();

            builder.Property(x => x.RowVersion)
                .IsRowVersion()
                .HasColumnName("xmin");

            builder.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
            builder.Property(a => a.UpdatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(a => new { a.ApplicantUserId, a.CycleId }).IsUnique();

            builder.HasIndex(a => new { a.CycleId, a.Status });

            builder.HasOne(x => x.Cycle)
                .WithMany()
                .HasForeignKey(x => x.CycleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

}
