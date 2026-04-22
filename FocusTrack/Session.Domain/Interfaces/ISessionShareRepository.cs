using Session.Domain.Entities;

namespace Session.Domain.Interfaces
{
    public interface ISessionShareRepository
    {
        Task AddAsync(SessionShare share, CancellationToken ct = default);
        Task<bool> ExistsAsync(Guid sessionId, string userId, CancellationToken ct = default);
    }
}
