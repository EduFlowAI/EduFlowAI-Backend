using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.AI.Application.DbContextAbstraction;
using EduFlowAI.Communication.Application.DbContextAbstraction;
using EduFlowAI.Documents.Application.DbContextAbstraction;
using EduFlowAI.Identity.Application.DbContextAbstraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Persistence.Extensions
{
    public static class PersistenceDependencyInjection
    {
        public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
        {
            services.AddDbContext<EduFlowAIDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    npgsqlOptions =>
                    {
                        // Enable vector operations for Npgsql
                        npgsqlOptions.UseVector();
                    });
            });

            services.AddScoped<IIdentityDbContext>(sp =>
                sp.GetRequiredService<EduFlowAIDbContext>());

            services.AddScoped<IAdmissionDbContext>(sp =>
                sp.GetRequiredService<EduFlowAIDbContext>());

            services.AddScoped<IDocumentDbContext>(sp =>
                sp.GetRequiredService<EduFlowAIDbContext>());

            services.AddScoped<IAIDbContext>(sp =>
                sp.GetRequiredService<EduFlowAIDbContext>());

            services.AddScoped<ICommunicationDbContext>(sp =>
                sp.GetRequiredService<EduFlowAIDbContext>());

            return services;
        }
    }
}
