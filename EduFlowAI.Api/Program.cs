using EduFlowAI.Admission;
using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.AI;
using EduFlowAI.AI.Application.DbContextAbstraction;
using EduFlowAI.Communication;
using EduFlowAI.Communication.Application.DbContextAbstraction;
using EduFlowAI.Documents;
using EduFlowAI.Documents.Application.DbContextAbstraction;
using EduFlowAI.Identity;
using EduFlowAI.Identity.Application.DbContextAbstraction;
using EduFlowAI.Persistence;
using EduFlowAI.Persistence.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

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
            builder.Services.AddEndpointsApiExplorer();

            // Register EduFlowAIDbContext with Npgsql provider, and DbContext interfaces
            builder.Services.AddPersistence(builder.Configuration);

            // register modules
            var identityBuilder = builder.Services.AddIdentityModule();
            identityBuilder.AddEntityFrameworkStores<EduFlowAIDbContext>();
            builder.Services.AddAdmissionModule();
            builder.Services.AddDocumentsModule();
            builder.Services.AddAIModule();
            builder.Services.AddCommunicationModule();

            #region Swagger
            builder.Services.AddSwaggerGen(options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "EduFlowAI API",
                        Version = "v1"
                    });
                    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Description = "Please enter a valid token (without writing bearer)",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.Http,
                        Scheme = JwtBearerDefaults.AuthenticationScheme,
                        BearerFormat = "JWT"
                    });
                    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
                    });
                }); 
            #endregion

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();

                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();


            app.Run();
        }
    }
}
