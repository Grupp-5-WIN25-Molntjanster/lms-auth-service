using Lms.Auth.Infrastructure;
using Lms.Auth.Domain.Entities;
using Lms.Auth.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lms.Auth.Infrastructure.Persistence;

public class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(AuthDbContext context) : base(context) { }

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        return await _dbSet.Include(rt => rt.User)
                           .FirstOrDefaultAsync(rt => rt.Token == token);
    }

    public async Task RemoveAllForUserAsync(int userId)
    {
        var tokens = await _dbSet.Where(rt => rt.UserId == userId).ToListAsync();
        _dbSet.RemoveRange(tokens);
    }
}