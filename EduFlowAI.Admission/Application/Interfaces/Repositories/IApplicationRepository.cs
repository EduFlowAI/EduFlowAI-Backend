using System;
using System.Threading.Tasks;

namespace EduFlowAI.Admission.Application.Interfaces.Repositories
{
    public interface IApplicationRepository
    {
        /// <summary>
        /// Checks if a user has an existing application for a given admission cycle.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cycleId"></param>
        /// <returns></returns>
        Task<bool> HasExistingApplicationAsync(string userId, Guid cycleId);

        /// <summary>
        /// Adds a new application to the database.
        /// </summary>
        /// <param name="application"></param>
        /// <returns></returns>
        Task AddAsync(Domain.Entities.Application application);

        /// <summary>
        /// Commits the changes to the database.
        /// </summary>
        /// <returns></returns>
        Task SaveChangesAsync();
    }
}
