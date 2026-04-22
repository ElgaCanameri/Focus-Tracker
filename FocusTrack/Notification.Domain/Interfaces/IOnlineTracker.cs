namespace Notification.Application.Interfaces;

// tracks who is currently connected via SignalR
public interface IOnlineTracker
{
    Task<bool> IsOnlineAsync(string userId);
}