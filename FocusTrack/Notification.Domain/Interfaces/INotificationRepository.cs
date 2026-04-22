namespace Notification.Domain.Interfaces;

public interface INotificationRepository
{
    Task<Entities.Notification?> GetByUserIdAsync(
        string userId, CancellationToken ct = default);
    Task AddAsync(
       Entities.Notification preference, CancellationToken ct = default);
    void Update(Entities.Notification preference);
}