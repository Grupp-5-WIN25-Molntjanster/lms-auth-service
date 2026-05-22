using Lms.Auth.Domain.Common;

namespace Lms.Auth.Domain.Entities;

/// <summary>
/// A long-lived token that allows the client to request a new access token
/// without re-entering credentials. Stored in the database to enable revocation.
/// </summary>
public class RefreshToken : BaseEntity
{
    private RefreshToken() { }

    public RefreshToken(string token, DateTime expiresAt, User user)
    {
        Token = token;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
        IsRevoked = false;
        User = user;
        UserId = user.Id;
    }

    public string Token { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }

    // Foreign key back to the owning user.
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    /// <summary>
    /// True if the token has passed its expiration date.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Marks this token as revoked, preventing further use.
    /// </summary>
    public void Revoke()
    {
        IsRevoked = true;
        UpdatedAt = DateTime.UtcNow;
    }
}