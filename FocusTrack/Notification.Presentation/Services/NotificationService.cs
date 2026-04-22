using Microsoft.AspNetCore.SignalR;
using Notification.Application.Interfaces;
using Notification.Presentation.Hubs;

namespace Notification.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IEmailService _emailService;
        private readonly IOnlineTracker _onlineTracker;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IHubContext<NotificationHub> hubContext,
            IEmailService emailService,
            IOnlineTracker onlineTracker,
            ILogger<NotificationService> logger)
        {
            _hubContext = hubContext;
            _emailService = emailService;
            _onlineTracker = onlineTracker;
            _logger = logger;
        }
        public async Task SendRealtimeAsync(
        string userId, string message, CancellationToken ct = default)
        {
            await _hubContext.Clients
                .Group(userId.ToString())
                .SendAsync("ReceiveNotification", message, ct);

            _logger.LogInformation("Realtime notification sent to User {UserId}", userId);
        }

        public async Task SendEmailAsync(string email,string replyTo, string subject, string message, CancellationToken ct = default)
        {
            await _emailService.SendAsync(email,replyTo, subject, message, ct);

            _logger.LogInformation("Email notification sent to {Email}", email);
        }
    }
}
