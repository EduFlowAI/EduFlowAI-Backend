using EduFlowAI.Communication.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Communication.Application.Interfaces
{
    public interface INotificationService
    {
        Task NotifyAsync(CreateNotificationRequest request, CancellationToken cancellationToken);
    }

    public sealed record CreateNotificationRequest(
        string UserId,
        Guid? ApplicationId,
        NotificationType Type,
        string Message);

}
