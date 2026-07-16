using EduFlowAI.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Identity
{
    public static class IdentityModule
    {
        public static IdentityBuilder AddIdentityModule(this IServiceCollection services)
        {
            // Application + Infrastructure services registrations go here later


            return services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddSignInManager();
        }

        // Called only by Api — Worker never calls this
        // public static IEndpointRouteBuilder MapIdentityPresentation(this IEndpointRouteBuilder app) { ... }
    }
}
