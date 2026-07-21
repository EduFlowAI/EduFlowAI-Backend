using EduFlowAI.Admission;
using EduFlowAI.AI;
using EduFlowAI.Communication;
using EduFlowAI.Documents;
using EduFlowAI.Persistence;
using EduFlowAI.Persistence.Extensions;
using EduFlowAI.Shared.Messaging.Contracts.Configuration;
using iText.StyledXmlParser.Jsoup.Nodes;
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
            builder.Services.AddCommunicationModule();

            // messaging -> ensure running rabbitmq before un-comment it
            //builder.ConfigureCommonMessaging("EduFlowAI.Worker");
            builder.Services.ConfigureWolverine(options =>
            {
                MessagingConfiguration.ConfigureWorkerListeners(options);
            });

            // we will add consumers later
            //builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
