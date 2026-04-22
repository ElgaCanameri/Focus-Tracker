using Contracts.Events;
using MassTransit;
using Notification.Domain.Interfaces;

namespace Notification.Presentation.Consumers;

public class DailyGoalAchievedConsumer : IConsumer<DailyGoalAchievedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly INotificationRepository _preferenceRepository;
    private readonly ILogger<DailyGoalAchievedConsumer> _logger;

    public DailyGoalAchievedConsumer(
        INotificationService notificationService,
        INotificationRepository preferenceRepository,
        ILogger<DailyGoalAchievedConsumer> logger)
    {
        _notificationService = notificationService;
        _preferenceRepository = preferenceRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DailyGoalAchievedEvent> context)
    {
        var evt = context.Message;

        var preference = await _preferenceRepository
            .GetByUserIdAsync(evt.UserId, context.CancellationToken);

        if (preference is null)
        {
            _logger.LogWarning(
                "No preference found for User {UserId}", evt.UserId);
            return;
        }

        var message = $"Congratulations! You have reached your daily focus goal of 120 minutes!";

        if (preference.IsOnline)
        {
            await _notificationService.SendRealtimeAsync(
                evt.UserId, message, context.CancellationToken);
        }
        else
        {
            await _notificationService.SendEmailAsync(
                preference.Email,
                null,
                "Daily Focus Goal Achieved!",
                message,
                context.CancellationToken);
        }
    }
}