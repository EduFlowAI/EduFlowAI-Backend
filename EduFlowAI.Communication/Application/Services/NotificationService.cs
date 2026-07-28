using EduFlowAI.Communication.Application.DbContextAbstraction;
using EduFlowAI.Communication.Application.Interfaces;
using EduFlowAI.Communication.Domain.Entities;
using EduFlowAI.Shared.Kernel.Messaging;
using EduFlowAI.Shared.Messaging.Contracts.Communication.V1;
using System;
using System.Collections.Generic;
using System.Text;
using Wolverine.EntityFrameworkCore;

namespace EduFlowAI.Communication.Application.Services
{
    public sealed class NotificationService(ICommunicationDbContext _dbContext, IOutboxPublisher _outboxPublisher) 
        : INotificationService
    {
        public async Task NotifyAsync(CreateNotificationRequest request, CancellationToken cancellationToken)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                ApplicationId = request.ApplicationId,
                Type = request.Type,
                Message = request.Message,
                IsRead = false,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Notifications.Add(notification);

            await _outboxPublisher.PublishAsync(new NotificationCreatedV1(
                MessageId: Guid.NewGuid(),
                CorrelationId: request.ApplicationId ?? notification.Id,
                CausationId: null,
                NotificationId: notification.Id,
                UserId: notification.UserId,
                ApplicationId: notification.ApplicationId,
                NotificationType: notification.Type.ToString(),
                Message: notification.Message,
                CreatedAtUtc: notification.CreatedAt,
                OccurredAtUtc: DateTimeOffset.UtcNow));

            //await _outboxPublisher.SaveChangesAndFlushMessagesAsync(cancellationToken);
        }
    }
}
