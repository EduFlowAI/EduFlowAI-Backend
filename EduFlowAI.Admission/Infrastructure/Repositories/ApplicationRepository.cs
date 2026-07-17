using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Admission.Infrastructure.Repositories
{
    public class ApplicationRepository : IApplicationRepository
    {
        private readonly IAdmissionDbContext _dbContext;

        public ApplicationRepository(IAdmissionDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> HasExistingApplicationAsync(string userId, Guid cycleId)
        {
            // Check if there is any application matching the given user ID and cycle ID
            return await _dbContext.Applications.AnyAsync(a => a.ApplicantUserId == userId && a.CycleId == cycleId);
        }

        public async Task AddAsync(Domain.Entities.Application application)
        {
            await _dbContext.Applications.AddAsync(application);
        }
        
        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
