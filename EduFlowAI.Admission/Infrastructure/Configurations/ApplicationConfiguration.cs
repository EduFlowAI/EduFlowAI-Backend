using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlowAI.Admission.Infrastructure.Configurations
{
    public class ApplicationConfiguration : IEntityTypeConfiguration<Domain.Entities.Application>
    {
        public void Configure(EntityTypeBuilder<Domain.Entities.Application> builder)
        {
            builder.ToTable("Applications", schema: "admissions");

            builder.HasKey(a => a.Id);

            builder.Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

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
