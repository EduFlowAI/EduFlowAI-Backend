using EduFlowAI.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Identity.Application.DbContextAbstraction
{
    public interface IIdentityDbContext
    {
        DbSet<ApplicantProfile> ApplicantProfiles { get; }

        Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}
