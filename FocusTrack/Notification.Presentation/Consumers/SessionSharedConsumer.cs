using MassTransit;
using Notification.Domain.Interfaces;

namespace Notification.Presentation.Consumers;

public class SessionSharedConsumer : IConsumer<Contracts.Events.SessionSharedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly INotificationRepository _preferenceRepository;
    private readonly ILogger<SessionSharedConsumer> _logger;

    public SessionSharedConsumer(
        INotificationService notificationService,
        INotificationRepository preferenceRepository,
        ILogger<SessionSharedConsumer> logger)
    {
        _notificationService = notificationService;
        _preferenceRepository = preferenceRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<Contracts.Events.SessionSharedEvent> context)
    {
        var evt = context.Message;
        var ownerPreference = await _preferenceRepository.GetByUserIdAsync(evt.OwnerUserId, context.CancellationToken);
        var ownerEmail = ownerPreference?.Email ?? "system@focustrack.local";

        foreach (var recipientId in evt.RecipientUserIds)
        {
            var preference = await _preferenceRepository
                .GetByUserIdAsync(recipientId, context.CancellationToken);

            if (preference is null)
            {
                _logger.LogWarning(
                    "No preference found for User {UserId}", recipientId);
                continue;
            }

            var message = $"A session has been shared with you by User {evt.OwnerUserId}";

            if (preference.IsOnline)
            {
                // via SignalR
                await _notificationService.SendRealtimeAsync(
                    recipientId, message, context.CancellationToken);
            }
            else
            {
                _logger.LogInformation("Sending email to: '{Email}' for user: '{UserId}'", preference.Email, recipientId);
                // fallback email
                await _notificationService.SendEmailAsync(
                    preference.Email,
                    ownerEmail,
                    "A session has been shared with you",
                    message,
                    context.CancellationToken);
            }
        }
    }
}