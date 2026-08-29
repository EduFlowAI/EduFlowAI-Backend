using EduFlowAI.Identity.Application.DbContextAbstraction;
using EduFlowAI.Identity.Application.Interfaces;
using EduFlowAI.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Identity.Application.Services
{
    public class UserContactInfoReader(IIdentityDbContext dbContext) : IUserContactInfoReader
    {
        public async Task<UserContactInfo?> GetContactInfoAsync(string userId, CancellationToken cancellationToken)
        {
            var user = await dbContext.AppUsers.FindAsync(userId, cancellationToken);
            if (user == null)
            {
                return null;
            }

            var info = new UserContactInfo(
                UserId: user.Id,
                Email: user.Email!,
                IsActive: user.IsActive
            );

            return info;

        }

        public async Task<IReadOnlyList<UserContactInfo>> GetBatchContactInfoAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            var users = await dbContext.AppUsers
                .AsNoTracking().Where(u => userIds.Contains(u.Id))
                .ToListAsync(cancellationToken);

            return users.Select(u => new UserContactInfo(
                UserId: u.Id, Email: u.Email!, IsActive: u.IsActive
            )).ToList();
        }
    }
}
