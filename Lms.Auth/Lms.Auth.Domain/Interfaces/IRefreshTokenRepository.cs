using Lms.Auth.Domain.Entities;

namespace Lms.Auth.Domain.Interfaces;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task RemoveAllForUserAsync(int userId);
}