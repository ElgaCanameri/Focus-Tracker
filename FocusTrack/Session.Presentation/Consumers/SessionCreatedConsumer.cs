using Contracts.Events;
using MassTransit;
using Session.Application.Common;
using Session.Domain.Interfaces;

namespace Session.Presentation.Consumers
{
    public class SessionCreatedConsumer : IConsumer<SessionCreatedEvent>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISessionRepository _sessionRepository;
        private readonly ILogger<SessionCreatedConsumer> _logger;

        public SessionCreatedConsumer(
            IUnitOfWork unitOfWork,
            ISessionRepository sessionRepository,
            ILogger<SessionCreatedConsumer> logger)
        {
            _unitOfWork = unitOfWork;
            _sessionRepository = sessionRepository;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SessionCreatedEvent> context)
        {
            var evt = context.Message;
            _logger.LogInformation("Processing stats for User: {UserId}", evt.UserId);

            try
            {
                await _sessionRepository.UpdateMonthlyStatisticsAsync(
                    evt.UserId,
                    evt.OccurredOn.Year,
                    evt.OccurredOn.Month,
                    evt.DurationMin,
                    context.CancellationToken);

                await _unitOfWork.SaveChangesAsync(context.CancellationToken);

                _logger.LogInformation("Successfully updated projection for {UserId}", evt.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update statistics");
                throw; // Re-throwing lets MassTransit handle retries/DLQ
            }
        }
    }
}
