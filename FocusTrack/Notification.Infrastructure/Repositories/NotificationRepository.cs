using Microsoft.EntityFrameworkCore;
using Notification.Domain.Interfaces;

namespace Notification.Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        public readonly NotificationDbContext _notificationDbContext;

        public NotificationRepository(NotificationDbContext notificationDbContext)
        {
            _notificationDbContext = notificationDbContext;
        }

        public async Task AddAsync(Domain.Entities.Notification notification, CancellationToken ct = default)
        {
            await _notificationDbContext.Notification.AddAsync(notification, ct);
        }

        public async Task<Domain.Entities.Notification?> GetByUserIdAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;
            return await _notificationDbContext.Notification.FirstOrDefaultAsync(n => n.UserId == userId, ct);

        }

        public void Update(Domain.Entities.Notification notification)
        {
            _notificationDbContext.Notification.Update(notification);
        }
    }
}
