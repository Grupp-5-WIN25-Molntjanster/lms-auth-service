using Lms.Auth.Domain.Common;
using Lms.Auth.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lms.Auth.Infrastructure.Persistence;

/// <summary>
/// Generic repository implementation using EF Core.
/// Provides basic CRUD and an IQueryable for advanced querying (pagination, filtering).
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AuthDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AuthDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);
    public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();
    public void Add(T entity) => _dbSet.Add(entity);
    public void Update(T entity) => _dbSet.Update(entity);
    public void Remove(T entity) => _dbSet.Remove(entity);
    public IQueryable<T> Query() => _dbSet.AsQueryable();
}