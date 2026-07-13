using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.AI
{
    public static class AIModule
    {
        public static IServiceCollection AddAIModule(this IServiceCollection services)
        {
            // Application + Infrastructure services registrations go here later
            return services;
        }

        // Called only by Api — Worker never calls this
        // public static IEndpointRouteBuilder MapAIPresentation(this IEndpointRouteBuilder app) { ... }
    }
}
