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

            // Must match Gemini:EmbeddingDimensions. 1536 retains far more of the embedding
            // model's 3072-dimension signal than 768 while staying under pgvector's 2000-dimension
            // ANN index limit, so an HNSW/IVFFlat index remains possible if the corpus grows.
            // Changing this requires a migration plus a full re-sync (vectors change width).
            builder.Property(c => c.Embedding).HasColumnType("vector(1536)").IsRequired();

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
