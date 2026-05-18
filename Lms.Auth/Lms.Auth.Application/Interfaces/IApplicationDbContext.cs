using Lms.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Auth.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}