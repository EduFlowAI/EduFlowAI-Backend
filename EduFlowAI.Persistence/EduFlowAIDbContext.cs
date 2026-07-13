using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Persistence
{
    public class EduFlowAIDbContext : DbContext
    {
        public EduFlowAIDbContext(DbContextOptions<EduFlowAIDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Module entity configs applied here
            // modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityModule).Assembly);
            // modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdmissionsModule).Assembly);
            // modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentsModule).Assembly);
            // modelBuilder.ApplyConfigurationsFromAssembly(typeof(AIModule).Assembly);
            // modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunicationModule).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
