public interface INotificationService
{
    Task SendRealtimeAsync(string userId, string message, CancellationToken ct = default);
    Task SendEmailAsync(string email,string replyTo, string subject, string message, CancellationToken ct = default);
}