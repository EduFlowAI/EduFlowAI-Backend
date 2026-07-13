using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Admission
{
    public static class AdmissionModule
    {
        public static IServiceCollection AddAdmissionModule(this IServiceCollection services)
        {
            // Application + Infrastructure services registrations go here later
            return services;
        }

        // Called only by Api — Worker never calls this
        // public static IEndpointRouteBuilder MapAdmissionPresentation(this IEndpointRouteBuilder app) { ... }
    }
}
