using Lms.Auth.Domain.Common;

namespace Lms.Auth.Domain.Interfaces;

/// <summary>
/// Generic repository interface providing standard data access operations.
/// Each entity-specific repository inherits from this.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    void Add(T entity);
    void Update(T entity);
    void Remove(T entity);
    IQueryable<T> Query();   // for custom filtering / pagination
}