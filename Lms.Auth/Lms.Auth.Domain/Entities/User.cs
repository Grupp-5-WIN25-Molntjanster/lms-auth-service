using Lms.Auth.Domain.Common;

namespace Lms.Auth.Domain.Entities;

/// <summary>
/// Represents a registered user in the LMS system.
/// The password hash is stored using BCrypt – we never store raw passwords.
/// The Role field determines access rights (Student, Instructor, Admin).
/// </summary>
public class User : BaseEntity
{
    // Private parameterless constructor required by EF Core.
    private User() { }

    public User(string email, string passwordHash, string firstName, string lastName, string role = "Student")
    {
        Email = email.ToLowerInvariant().Trim();
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        Role = role;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        EmailConfirmed = false;
    }

    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    // ============================================================
    // EMAIL VERIFICATION FIELDS
    // ============================================================
    public bool EmailConfirmed { get; private set; }
    public string? VerificationCode { get; private set; }
    public DateTime? VerificationCodeExpiresAt { get; private set; }

    // Navigation property: a user can have many refresh tokens.
    private readonly List<RefreshToken> _refreshTokens = new();
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();


    // ============================================================
    // EMAIL VERIFICATION METHODS
    // ============================================================

    /// <summary>
    /// Sets a new verification code with expiry.
    /// Code is valid for 1 hour.
    /// </summary>
    public void SetVerificationCode(string code, DateTime expiresAt)
    {
        VerificationCode = code;
        VerificationCodeExpiresAt = expiresAt;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Confirms the email and clears the verification code.
    /// </summary>
    public void ConfirmEmail()
    {
        EmailConfirmed = true;
        VerificationCode = null;
        VerificationCodeExpiresAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if the verification code has expired.
    /// </summary>
    public bool IsVerificationCodeExpired =>
        VerificationCodeExpiresAt.HasValue && DateTime.UtcNow > VerificationCodeExpiresAt.Value;

    /// <summary>
    /// Whether the user can log in.
    /// </summary>
    public bool CanLogin => IsActive && EmailConfirmed;


    /// <summary>
    /// Records a successful login time.
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Attaches a new refresh token to this user.
    /// </summary>
    public void AddRefreshToken(RefreshToken token)
    {
        _refreshTokens.Add(token);
    }

    /// <summary>
    /// Revokes a specific refresh token (used on logout or refresh rotation).
    /// </summary>
    public void RevokeRefreshToken(string tokenValue)
    {
        var token = _refreshTokens.FirstOrDefault(t => t.Token == tokenValue && !t.IsRevoked);
        token?.Revoke();
    }
}