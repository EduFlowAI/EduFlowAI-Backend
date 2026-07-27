using EduFlowAI.Admission;
using EduFlowAI.AI;
using EduFlowAI.Communication;
using EduFlowAI.Documents;
using EduFlowAI.Identity;
using EduFlowAI.Identity.Application.Interfaces;
using EduFlowAI.Identity.Application.Services;
using EduFlowAI.Persistence;
using EduFlowAI.Persistence.Extensions;
using EduFlowAI.Shared.Messaging.Contracts.Configuration;
using iText.StyledXmlParser.Jsoup.Nodes;
using JasperFx.Resources;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace EduFlowAI.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddPersistence(builder.Configuration);

            builder.Services.AddAdmissionModule();
            builder.Services.AddDocumentsModule();
            builder.Services.AddAIModule(builder.Configuration);
            builder.Services.AddCommunicationModule(builder.Configuration);

            builder.Services.AddScoped<IUserContactInfoReader, UserContactInfoReader>();

            // messaging -> ensure running rabbitmq before un-comment it
            builder.ConfigureCommonMessaging("EduFlowAI.Worker");
            builder.Services.ConfigureWolverine(options =>
            {
                MessagingConfiguration.ConfigureWorkerListeners(options);
            });

            // messaging dev only
            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddResourceSetupOnStartup();
            }

            // we will add consumers later
            //builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
