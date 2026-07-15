using Microsoft.AspNetCore.Identity;
using System;

namespace EduFlowAI.Identity.Domain.Entities
{
    // Inheriting from IdentityUser<Guid> automatically gives us Id, Email, PasswordHash, etc.
    public sealed class AppUser : IdentityUser<Guid>
    {
        public bool IsActive { get; set; } = true;
        public string PreferredLanguage { get; set; } = "en";
        public bool GmailNotificationsEnabled { get; set; } = false;

        // Explicit timestamp fields as requested
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeactivatedAt { get; set; } = DateTimeOffset.UtcNow;
        
        // Navigation property (1-to-1 relationship with Profile)
        public ApplicantProfile? ApplicantProfile { get; set; }
    }
}
