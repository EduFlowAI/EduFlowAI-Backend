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

            // Bind the relationship to the actual navigation properties
            // (KnowledgeBaseChunk.Document <-> KnowledgeBaseDocument.Chunks) so EF maps a
            // SINGLE foreign key on KnowledgeBaseDocumentId. Using the anonymous
            // HasOne<KnowledgeBaseDocument>().WithMany() form leaves those navigations
            // unmapped, and EF then discovers them as a second relationship with a shadow
            // "DocumentId" FK column — the duplicate-FK drift that caused insert failures
            // (Npgsql 23502: null value in column "KnowledgeBaseDocumentId").
            builder.HasOne(c => c.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.KnowledgeBaseDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
