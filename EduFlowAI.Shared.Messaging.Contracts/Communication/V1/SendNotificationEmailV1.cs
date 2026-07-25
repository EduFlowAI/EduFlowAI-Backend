using EduFlowAI.Shared.Messaging.Contracts.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using Wolverine.Attributes;

namespace EduFlowAI.Shared.Messaging.Contracts.Communication.V1
{
    [MessageIdentity("eduflow.communication.send-notification-email", Version = 1)]
    public sealed record SendNotificationEmailV1(
        Guid MessageId,
        Guid CorrelationId,
        Guid? CausationId,
        Guid NotificationId,
        string UserId,
        DateTimeOffset OccurredAtUtc
    ) : IIntegrationMessage;
}
