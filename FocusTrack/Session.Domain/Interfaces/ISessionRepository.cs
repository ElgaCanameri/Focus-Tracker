using Session.Domain.Entities;
using Session.Domain.Enums;

namespace Session.Domain.Interfaces
{
    public interface ISessionRepository
    {
        Task<Entities.Session?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<(IEnumerable<Entities.Session> Items, int TotalCount)> GetPagedAsync(
            string userId, int page, int pageSize, CancellationToken ct = default);
        Task<(IEnumerable<Entities.Session> Items, int TotalCount)> GetFilteredAsync(
           string? userId,
           SessionMode? mode,
           DateTime? startDateFrom,
           DateTime? startDateTo,
           decimal? minDuration,
           decimal? maxDuration,
           int page,
           int pageSize,
           CancellationToken ct = default);
        Task<IEnumerable<MonthlyFocusEntity>> GetMonthlyStatisticsAsync(int page, int pageSize, CancellationToken ct = default);
        Task UpdateMonthlyStatisticsAsync(string userId, int year, int month, decimal duration, CancellationToken ct = default);
        Task<Entities.Session?> GetByPublicTokenAsync(
        string token, CancellationToken ct = default);
        Task AddAsync(Entities.Session session, CancellationToken ct = default);
        void Update(Entities.Session session);
        void Delete(Entities.Session session);
    }
}
