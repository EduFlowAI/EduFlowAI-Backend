using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Identity
{
    public static class IdentityModule
    {
        public static IServiceCollection AddIdentityModule(this IServiceCollection services)
        {
            // Application + Infrastructure services registrations go here later
            return services;
        }

        // Called only by Api — Worker never calls this
        // public static IEndpointRouteBuilder MapIdentityPresentation(this IEndpointRouteBuilder app) { ... }
    }
}
