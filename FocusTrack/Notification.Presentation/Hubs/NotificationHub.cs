using Notification.Infrastructure.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Notification.Presentation.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            // add to group so we can target this user
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            OnlineTracker.UserConnected(userId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
            OnlineTracker.UserDisconnected(userId);

        await base.OnDisconnectedAsync(exception);
    }
}
