using Lms.Auth.Infrastructure;
using Lms.Auth.Domain.Entities;
using Lms.Auth.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lms.Auth.Infrastructure.Persistence;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AuthDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant().Trim());
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _dbSet.AnyAsync(u => u.Email == email.ToLowerInvariant().Trim());
    }
}