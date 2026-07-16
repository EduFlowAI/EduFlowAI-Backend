using EduFlowAI.Identity.Domain.Enums;
using System;

namespace EduFlowAI.Identity.Domain.Entities
{
    // Stores the academic and personal payload used for admission eligibility
    public sealed class ApplicantProfile
    {
        // Explicit primary key and timestamps
        public Guid Id { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Foreign Key
        public string UserId { get; set; }
        public string FullNameEn { get; set; } = null!;
        public string FullNameAr { get; set; } = null!;
        public string NationalId { get; set; } = null!;
        public string Nationality { get; set; } = null!;

        public DateOnly DateOfBirth { get; set; }

        // Using Enums for strict type safety in code
        public Gender Gender { get; set; }
        public CumulativeGrade CumulativeGrade { get; set; }
        public MilitaryStatus? MilitaryStatus { get; set; }

        public string? Address { get; set; }
        public string? Governorate { get; set; }
        public string University { get; set; } = null!;
        public string Faculty { get; set; } = null!;
        public string DegreeLevel { get; set; } = null!;
        public string Major { get; set; } = null!;
        public int GraduationYear { get; set; }

        // Navigation property back to User
        public AppUser User { get; set; } = null!;
    }
}
