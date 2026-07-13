
using EduFlowAI.Admission;
using EduFlowAI.AI;
using EduFlowAI.Communication;
using EduFlowAI.Documents;
using EduFlowAI.Identity;
using EduFlowAI.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers()
                .AddApplicationPart(typeof(IdentityModule).Assembly)
                .AddApplicationPart(typeof(AdmissionModule).Assembly)
                .AddApplicationPart(typeof(DocumentsModule).Assembly)
                .AddApplicationPart(typeof(AIModule).Assembly)
                .AddApplicationPart(typeof(CommunicationModule).Assembly);
           
            builder.Services.AddOpenApi();

            builder.Services.AddDbContext<EduFlowAIDbContext>(opt =>
                opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            // register modules
            builder.Services.AddIdentityModule();
            builder.Services.AddAdmissionModule();
            builder.Services.AddDocumentsModule();
            builder.Services.AddAIModule();
            builder.Services.AddCommunicationModule();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();


            app.Run();
        }
    }
}
