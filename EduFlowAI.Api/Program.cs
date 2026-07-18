using EduFlowAI.Admission;
using EduFlowAI.AI;
using EduFlowAI.Api.Middlewares;
using EduFlowAI.Communication;
using EduFlowAI.Documents;
using EduFlowAI.Identity;
using EduFlowAI.Persistence;
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

            // Global exception handling: unhandled exceptions become safe ProblemDetails responses.
            builder.Services.AddExceptionHandler<ExceptionHandler>();
            builder.Services.AddProblemDetails();

            // Local manual-testing dashboard runs from a file:// page (Origin "null") or an
            // arbitrary localhost port. Applied in Development only - never in production.
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("LocalDevTesting", policy => policy
                    .SetIsOriginAllowed(_ => true)
                    .AllowAnyHeader()
                    .AllowAnyMethod());
            });

            // Register EduFlowAIDbContext with Npgsql provider
            builder.Services.AddDbContext<EduFlowAIDbContext>(options =>
            {
                options.UseNpgsql(
                    builder.Configuration.GetConnectionString("DefaultConnection"),
                    npgsqlOptions =>
                    {
                        // Enable vector operations for Npgsql
                        npgsqlOptions.UseVector();
                    });
            });

            // Expose the shared context as the base DbContext type so the AI module's
            // repository/controller can depend on DbContext without referencing
            // EduFlowAI.Persistence (which would create a circular project reference).
            builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<EduFlowAIDbContext>());

            // register modules
            var identityBuilder = builder.Services.AddIdentityModule();
            builder.Services.AddAdmissionModule();
            builder.Services.AddDocumentsModule();
            builder.Services.AddAIModule();
            builder.Services.AddCommunicationModule();

            identityBuilder.AddEntityFrameworkStores<EduFlowAIDbContext>();

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

            // Must be first so it wraps the whole pipeline and catches downstream exceptions.
            app.UseExceptionHandler();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                // Must run before endpoint routing so preflight requests get CORS headers.
                app.UseCors("LocalDevTesting");

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
