using EduFlowAI.AI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.AI.Infrastructure.Configurations
{
    public class KnowledgeBaseChunkConfiguration : IEntityTypeConfiguration<KnowledgeBaseChunk>
    {
        public void Configure(EntityTypeBuilder<KnowledgeBaseChunk> builder)
        {
            builder.ToTable("KnowledgeBaseChunk", schema: "ai", t =>
                t.HasCheckConstraint("CK_KnowledgeBaseChunk_Content_NotEmpty",
                    "length(btrim(\"Content\")) > 0"));

            builder.HasKey(c => c.Id);

            builder.Property(c => c.Content).HasColumnType("text").IsRequired();

            builder.Property(c => c.Embedding).HasColumnType("vector(768)").IsRequired();

            builder.HasOne<KnowledgeBaseDocument>()
                .WithMany()
                .HasForeignKey(c => c.KnowledgeBaseDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
