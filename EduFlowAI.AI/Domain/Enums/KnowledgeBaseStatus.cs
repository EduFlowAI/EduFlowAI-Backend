using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.AI.Domain.Enums
{
    public enum KnowledgeBaseStatus
    {
        Pending = 1,
        Indexing = 2,
        Indexed = 3,
        Failed = 4
    }
}
