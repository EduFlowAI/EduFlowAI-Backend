using EduFlowAI.Admission.Infrastructure.Configurations;
using EduFlowAI.Identity.Infrastructure.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Persistence
{
    // Inherit from IdentityDbContext to include ASP.NET Core Identity tables out of the box
    public class EduFlowAIDbContext : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>
    {
        public EduFlowAIDbContext(DbContextOptions<EduFlowAIDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {        
            base.OnModelCreating(modelBuilder);     // Create Identity tables

            // Enable pgvector extension for PostgreSQL to support AI embeddings
            modelBuilder.HasPostgresExtension("vector");

            // 3. Module entity configs applied here (The Modular Monolith Way)
            // Note: We will replace 'IdentityModule' with an actual Entity class name (e.g., AppUser) once created.       
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppUserConfiguration).Assembly);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProgramConfiguration).Assembly);
            // modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentsModule).Assembly);
            // modelBuilder.ApplyConfigurationsFromAssembly(typeof(AIModule).Assembly);
            // modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunicationModule).Assembly);

        }
    }
}
