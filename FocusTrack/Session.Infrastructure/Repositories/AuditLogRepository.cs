using Session.Domain.Entities;
using Session.Domain.Interfaces;

namespace Session.Infrastructure.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly AppDbContext _context;

        public AuditLogRepository(AppDbContext context) => _context = context;

        public async Task AddAsync(AuditLog log, CancellationToken ct = default)
            => await _context.AuditLogs.AddAsync(log, ct);
    }
}
