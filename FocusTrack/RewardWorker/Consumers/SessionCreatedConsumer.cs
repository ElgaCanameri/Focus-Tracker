using Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using RewardWorker.Services;

namespace RewardWorker.Consumers
{
    public class SessionCreatedConsumer : IConsumer<Contracts.Events.SessionCreatedEvent>
    {
        private readonly DailyGoalEvaluator _evaluator;
        private readonly RewardDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<SessionCreatedConsumer> _logger;

        public SessionCreatedConsumer(
        DailyGoalEvaluator evaluator,
        RewardDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<SessionCreatedConsumer> logger)
        {
            _evaluator = evaluator;
            _context = context;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SessionCreatedEvent> context)
        {
            var evt = context.Message;

            _logger.LogInformation(
                "Evaluating daily goal for User {UserId} after session {SessionId}",
                evt.UserId, evt.SessionId);

            var (goalReached, triggeringSessionId) = await _evaluator.EvaluateAsync(
                evt.UserId,
                evt.OccurredOn,
                context.CancellationToken);

            if (!goalReached || triggeringSessionId is null)
                return;

            // mark the triggering session
            var session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.Id == triggeringSessionId,
                    context.CancellationToken);

            if (session is null)
                return;

            session.IsDailyGoalAchieved = true;
            await _context.SaveChangesAsync(context.CancellationToken);

            // publish event for Notification Service
            await _publishEndpoint.Publish(new DailyGoalAchievedEvent(
                triggeringSessionId.Value,
                evt.UserId,
                120m,
                DateTime.UtcNow));

            _logger.LogInformation(
                "Daily goal achieved for User {UserId}. Session {SessionId} marked.",
                evt.UserId, triggeringSessionId);
        }
    }
}
