using EduFlowAI.Communication.Application.Interfaces;
using EduFlowAI.Communication.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Communication
{
    public static class CommunicationModule
    {
        public static IServiceCollection AddCommunicationModule(this IServiceCollection services)
        {
            services.AddScoped<INotificationService, NotificationService>();

            return services;
        }

        // Called only by Api — Worker never calls this
        // public static IEndpointRouteBuilder MapCommunicationPresentation(this IEndpointRouteBuilder app) { ... }
    }
}
