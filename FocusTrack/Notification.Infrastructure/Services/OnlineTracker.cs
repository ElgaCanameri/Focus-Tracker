using Notification.Application.Interfaces;
using System.Collections.Concurrent;

namespace Notification.Infrastructure.Services;

// tracks connected SignalR users in memory
public class OnlineTracker : IOnlineTracker
{
    private static readonly ConcurrentDictionary<string, int> _connections = new();

    public Task<bool> IsOnlineAsync(string userId)
        => Task.FromResult(_connections.ContainsKey(userId));

    public static void UserConnected(string userId)
        => _connections.AddOrUpdate(userId, 1, (_, count) => count + 1);

    public static void UserDisconnected(string userId)
    {
        if (_connections.TryGetValue(userId, out var count) && count <= 1)
            _connections.TryRemove(userId, out _);
        else
            _connections.TryUpdate(userId, count - 1, count);
    }
}