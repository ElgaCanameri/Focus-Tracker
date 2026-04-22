using Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Notification.Infrastructure;

namespace Notification.Presentation.Consumers
{
    public class UserLoggedInConsumer : IConsumer<UserLoggedInEvent>
    {
        private readonly NotificationDbContext _appDbContext;

        public UserLoggedInConsumer(NotificationDbContext db) => _appDbContext = db;

        public async Task Consume(ConsumeContext<UserLoggedInEvent> context)
        {
            var exists = await _appDbContext.Notification
                .AnyAsync(n => n.UserId == context.Message.UserId);

            if (!exists)
            {
                var notification = Domain.Entities.Notification
                     .Create(context.Message.UserId, context.Message.Email);

                _appDbContext.Notification.Add(notification);
                await _appDbContext.SaveChangesAsync();
            }
        }
    }
}
