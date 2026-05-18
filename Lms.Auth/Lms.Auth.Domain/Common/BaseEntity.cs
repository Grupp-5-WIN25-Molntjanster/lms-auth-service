namespace Lms.Auth.Domain.Common;

/// <summary>
/// Base class for all domain entities.
/// Provides a common identity (Id) and creation timestamp.
/// This allows us to treat all entities uniformly (e.g., for auditing or generic repositories).
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    // Not every entity needs UpdatedAt, but it's here if we later add auditing.
    public DateTime? UpdatedAt { get; protected set; }
}