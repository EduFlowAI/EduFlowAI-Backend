using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Documents.Domain.Enums
{
    public enum DocumentStatus
    {
        Uploaded = 1,
        Verifying = 2,
        Approved = 3,
        NeedsHumanReview = 4,
        Rejected = 5
    }
}
