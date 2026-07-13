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
            // Application + Infrastructure services registrations go here later
            return services;
        }

        // Called only by Api — Worker never calls this
        // public static IEndpointRouteBuilder MapCommunicationPresentation(this IEndpointRouteBuilder app) { ... }
    }
}
