using EduFlowAI.Admission;
using EduFlowAI.AI;
using EduFlowAI.Communication;
using EduFlowAI.Documents;
using EduFlowAI.Persistence;
using EduFlowAI.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

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

            // we will add consumers later
            //builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
