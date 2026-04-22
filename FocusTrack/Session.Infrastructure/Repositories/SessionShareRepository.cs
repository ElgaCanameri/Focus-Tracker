using Microsoft.EntityFrameworkCore;
using Session.Domain.Entities;
using Session.Domain.Interfaces;

namespace Session.Infrastructure.Repositories
{
    public class SessionShareRepository : ISessionShareRepository
    {
        private readonly AppDbContext _context;

        public SessionShareRepository(AppDbContext context) => _context = context;

        public async Task AddAsync(SessionShare share, CancellationToken ct = default)
            => await _context.SessionShares.AddAsync(share, ct);

        public async Task<bool> ExistsAsync(
            Guid sessionId, string userId, CancellationToken ct = default)
            => await _context.SessionShares
                .AnyAsync(s =>
                    s.SessionId == sessionId &&
                    s.RecipientUserId == userId, ct);
    }
}
