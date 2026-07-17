using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Documents.Application.Interfaces
{
    internal interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, CancellationToken cancellationToken = default);
    }
}
