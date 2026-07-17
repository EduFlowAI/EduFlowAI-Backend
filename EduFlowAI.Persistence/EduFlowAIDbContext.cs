using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Infrastructure.Configurations;
using EduFlowAI.AI.Application.DbContextAbstraction;
using EduFlowAI.AI.Domain.Entities;
using EduFlowAI.AI.Infrastructure.Configurations;
using EduFlowAI.Communication.Application.DbContextAbstraction;
using EduFlowAI.Communication.Domain.Entities;
using EduFlowAI.Communication.Infrastructure.Configurations;
using EduFlowAI.Documents;
using EduFlowAI.Documents.Application.DbContextAbstraction;
using EduFlowAI.Documents.Domain.Entities;
using EduFlowAI.Documents.Infrastructure.Configurations;
using EduFlowAI.Identity.Application.DbContextAbstraction;
using EduFlowAI.Identity.Domain.Entities;
using EduFlowAI.Identity.Infrastructure.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using Wolverine.EntityFrameworkCore;

namespace EduFlowAI.Persistence
{
    public class EduFlowAIDbContext : IdentityDbContext<AppUser>, IAdmissionDbContext, IAIDbContext
        , ICommunicationDbContext, IDocumentDbContext, IIdentityDbContext
    {
        public EduFlowAIDbContext(DbContextOptions<EduFlowAIDbContext> options) : base(options) { }

        #region DbSets
        // Identity
        public DbSet<ApplicantProfile> ApplicantProfiles => Set<ApplicantProfile>();

        // Admission
        public DbSet<Institution> Institutions => Set<Institution>();
        public DbSet<Program> Programs => Set<Program>();
        public DbSet<ProgramDocumentRequirement> ProgramDocumentRequirements => Set<ProgramDocumentRequirement>();
        public DbSet<AdmissionCycle> AdmissionCycles => Set<AdmissionCycle>();
        public DbSet<CycleEligibilityRule> CycleEligibilityRules => Set<CycleEligibilityRule>();
        public DbSet<Track> Tracks => Set<Track>();
        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<TrackBranchOffering> TrackBranchOfferings => Set<TrackBranchOffering>();
        public DbSet<Application> Applications => Set<Application>();
        public DbSet<ApplicationPreference> ApplicationPreferences => Set<ApplicationPreference>();
        public DbSet<EligibilityResult> EligibilityResults => Set<EligibilityResult>();
        public DbSet<SimulatedStageResult> SimulatedStageResults => Set<SimulatedStageResult>();

        // Documents
        public DbSet<ApplicantDocument> ApplicantDocuments => Set<ApplicantDocument>();
        public DbSet<DocumentReplacementRequest> DocumentReplacementRequests => Set<DocumentReplacementRequest>();
        // AI and Knowledge Base
        public DbSet<KnowledgeBaseDocument> KnowledgeBaseDocuments => Set<KnowledgeBaseDocument>();
        public DbSet<KnowledgeBaseChunk> KnowledgeBaseChunks => Set<KnowledgeBaseChunk>();

        // Communication
        public DbSet<Notification> Notifications => Set<Notification>(); 
        #endregion

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {        
            base.OnModelCreating(modelBuilder);

            // Enable pgvector extension for PostgreSQL to support AI embeddings
            modelBuilder.HasPostgresExtension("vector");

            // mapping Wolverine messaging storage
            modelBuilder.MapWolverineEnvelopeStorage();

            // Module entity configs applied here (The Modular Monolith Way)
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppUserConfiguration).Assembly);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProgramConfiguration).Assembly);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicantDocumentConfiguration).Assembly);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(KnowledgeBaseDocumentConfiguration).Assembly);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationConfiguration).Assembly);

        }
    }
}
