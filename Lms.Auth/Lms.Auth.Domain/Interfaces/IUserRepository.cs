using Lms.Auth.Domain.Entities;

namespace Lms.Auth.Domain.Interfaces;

/// <summary>
/// Repository contract for User aggregate.
/// Extends a generic repository for basic CRUD, plus custom queries.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<bool> EmailExistsAsync(string email);
}