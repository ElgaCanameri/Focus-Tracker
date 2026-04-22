using Microsoft.EntityFrameworkCore;
using Session.Domain.Entities;
using Session.Domain.Interfaces;

namespace Session.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _appDbContext;

        public UserRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }
        public async Task AddAsync(User user, CancellationToken ct = default)
        {
            await _appDbContext.Users.AddAsync(user);
        }

        public async Task<User?> GetByExternalIdAsync(string externalId, CancellationToken ct = default)
        {
            return await _appDbContext.Users.FirstOrDefaultAsync(u => u.ExternalId == externalId, ct);
        }

        public async Task<User?> GetByIdAsync(string externalId, CancellationToken ct = default)
        {
            return await _appDbContext.Users.FirstOrDefaultAsync(u => u.ExternalId == externalId, ct);
        }

        public void Update(User user)
        {
            _appDbContext.Users.Update(user);
        }
    }
}
