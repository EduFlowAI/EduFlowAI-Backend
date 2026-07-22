using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Documents.Application.Services;
using EduFlowAI.Documents.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Documents
{
    public static class DocumentsModule
    {
        public static IServiceCollection AddDocumentsModule(this IServiceCollection services)
        {
            services.AddScoped<IDocumentHumanReviewService, DocumentHumanReviewService>();
            services.AddScoped<IFileStorageService, FileStorageService>();

            return services;
        }

        // Called only by Api — Worker never calls this
        // public static IEndpointRouteBuilder MapDocumentsPresentation(this IEndpointRouteBuilder app) { ... }
    }
}
