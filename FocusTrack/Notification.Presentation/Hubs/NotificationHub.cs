using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Notification.Application.Common;
using Notification.Domain.Interfaces;
using Notification.Infrastructure.Services;
using System.Security.Claims;

namespace Notification.Presentation.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            OnlineTracker.UserConnected(userId);

            using var scope = Context.GetHttpContext()!.RequestServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var pref = await repo.GetByUserIdAsync(userId);
            if (pref is not null)
            {
                pref.SetOnline();
                repo.Update(pref);
                await uow.SaveChangesAsync();
            }
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            OnlineTracker.UserDisconnected(userId);

            using var scope = Context.GetHttpContext()!.RequestServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var pref = await repo.GetByUserIdAsync(userId);
            if (pref is not null)
            {
                pref.SetOffline();
                repo.Update(pref);
                await uow.SaveChangesAsync();
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}
