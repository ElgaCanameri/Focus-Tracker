using Microsoft.EntityFrameworkCore;
using Session.Domain;
using Session.Domain.Entities;
using Session.Domain.Enums;
using Session.Domain.Interfaces;

namespace Session.Infrastructure.Repositories
{
    public class SessionRepository : ISessionRepository
    {
        private readonly AppDbContext _appDbContext;

        public SessionRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        #region Basic CRUD
        public async Task AddAsync(Domain.Entities.Session session, CancellationToken ct = default)
        {
            await _appDbContext.Sessions.AddAsync(session, ct);
        }
        public async Task<Domain.Entities.Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _appDbContext.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        }
        public async Task<(IEnumerable<Domain.Entities.Session> Items, int TotalCount)> GetPagedAsync(string userId, int page, int pageSize, CancellationToken ct = default)
        {
            var query = _appDbContext.Sessions
                .Where(s => s.UserId == userId // sessions they own
                || _appDbContext.SessionShares // sessions shared with them
                .Any(sh => sh.SessionId == s.Id
                && sh.RecipientUserId == userId));

            var total = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(s => s.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }
        public void Update(Domain.Entities.Session session)
        {
            _appDbContext.Sessions.Update(session);
        }
        public void Delete(Domain.Entities.Session session)
        {
            _appDbContext.Sessions.Remove(session);
        }
        public async Task UpdateMonthlyStatisticsAsync(string userId, int year, int month, decimal duration, CancellationToken ct = default)
        {
            var existingRecord = await _appDbContext.MonthlyFocusItems
                .FirstOrDefaultAsync(x => x.UserId == userId &&
                                          x.Year == year &&
                                          x.Month == month, ct);

            if (existingRecord == null)
            {
                var newStat = new MonthlyFocusEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Year = year,
                    Month = month,
                    TotalDurationMin = duration
                };
                await _appDbContext.MonthlyFocusItems.AddAsync(newStat, ct);
            }
            else
            {
                existingRecord.TotalDurationMin += duration;
            }

        }
        #endregion

        public async Task<Domain.Entities.Session?> GetByPublicTokenAsync(string token, CancellationToken ct = default)
        {
            return await _appDbContext.Sessions
                     .FirstOrDefaultAsync(s => s.PublicLinkToken == token, ct);
        }

        #region Admin Filtering
        public async Task<(IEnumerable<Domain.Entities.Session> Items, int TotalCount)> GetFilteredAsync(string? userId, SessionMode? mode, DateTime? startDateFrom, DateTime? startDateTo, decimal? minDuration, decimal? maxDuration, int page, int pageSize, CancellationToken ct = default)
        {
            var query = _appDbContext.Sessions.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(s => s.UserId == userId);

            if (mode.HasValue)
                query = query.Where(s => s.Mode == mode.Value);

            if (startDateFrom.HasValue)
                query = query.Where(s => s.StartTime >= startDateFrom.Value);

            if (startDateTo.HasValue)
                query = query.Where(s => s.StartTime <= startDateTo.Value);

            if (minDuration.HasValue)
                query = query.Where(s => s.DurationMin.Value >= minDuration.Value);

            if (maxDuration.HasValue)
                query = query.Where(s => s.DurationMin.Value <= maxDuration.Value);

            var total = await query.CountAsync(ct);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }
        public async Task<IEnumerable<MonthlyFocusEntity>> GetMonthlyStatisticsAsync(int page, int pageSize, CancellationToken ct = default)
        {
            return await _appDbContext.MonthlyFocusItems
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync(ct);
        }
        #endregion
    }
}
