using FluentAssertions;
using Lms.Auth.Application.Interfaces;
using Lms.Auth.Infrastructure.Security;

namespace Lms.Auth.UnitTests.Infrastructure.Security;

/// <summary>
/// Unit tests for BCrypt password hashing.
/// 
/// These tests verify:
/// - Hashing produces a valid BCrypt hash
/// - The same password always verifies against its hash
/// - Different passwords never verify against wrong hash
/// - BCrypt salts are unique (same password → different hashes)
/// 
/// BCrypt is an industry-standard password hashing algorithm that:
/// - Automatically handles salting (no manual salt management)
/// - Is intentionally slow (prevents brute-force attacks)
/// - Includes a work factor (cost) in the hash itself
/// </summary>
public class PasswordHasherTests
{
    private readonly PasswordHasher _passwordHasher;

    public PasswordHasherTests()
    {
        _passwordHasher = new PasswordHasher();
    }

    /// <summary>
    /// TEST: Hashing a password should return a non-empty string
    /// that starts with the BCrypt identifier "$2a$" or "$2b$".
    /// </summary>
    [Fact]
    public void Hash_ShouldReturnBcryptHash()
    {
        // ARRANGE
        var password = "MySecurePassword123!";

        // ACT
        var hash = _passwordHasher.Hash(password);

        // ASSERT
        hash.Should().NotBeNullOrEmpty("hashing should produce output");
        hash.Should().StartWith("$2", "BCrypt hashes start with the algorithm identifier");
        hash.Should().NotBe(password, "hash must NOT be the original password");
    }

    /// <summary>
    /// TEST: Verifying the correct password against its hash should return true.
    /// </summary>
    [Fact]
    public void Verify_WithCorrectPassword_ShouldReturnTrue()
    {
        // ARRANGE
        var password = "CorrectPassword123!";
        var hash = _passwordHasher.Hash(password);

        // ACT
        var result = _passwordHasher.Verify(password, hash);

        // ASSERT
        result.Should().BeTrue("correct password should verify successfully");
    }

    /// <summary>
    /// TEST: Verifying the wrong password should return false.
    /// 
    /// SECURITY: BCrypt uses constant-time comparison to prevent timing attacks.
    /// Even wrong passwords take the same amount of time to check as correct ones.
    /// </summary>
    [Fact]
    public void Verify_WithWrongPassword_ShouldReturnFalse()
    {
        // ARRANGE
        var correctPassword = "CorrectPassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = _passwordHasher.Hash(correctPassword);

        // ACT
        var result = _passwordHasher.Verify(wrongPassword, hash);

        // ASSERT
        result.Should().BeFalse("wrong password should not verify");
    }

    /// <summary>
    /// TEST: Two hashes of the same password should be different.
    /// 
    /// This proves that BCrypt uses a random salt for each hash.
    /// Salting prevents rainbow table attacks – even if two users
    /// have the same password, their hashes will be different.
    /// </summary>
    [Fact]
    public void Hash_SamePasswordTwice_ShouldProduceDifferentHashes()
    {
        // ARRANGE
        var password = "SamePassword123!";

        // ACT
        var hash1 = _passwordHasher.Hash(password);
        var hash2 = _passwordHasher.Hash(password);

        // ASSERT
        hash1.Should().NotBe(hash2,
            "BCrypt generates unique salts, so hashes should differ even for same password");
    }

    /// <summary>
    /// TEST: Empty password should still produce a valid hash.
    /// (Validation of empty passwords happens in the API layer via [Required] attribute)
    /// </summary>
    [Fact]
    public void Hash_EmptyPassword_ShouldStillHash()
    {
        // ACT
        var hash = _passwordHasher.Hash(string.Empty);

        // ASSERT
        hash.Should().NotBeNullOrEmpty("even empty strings should be hashable");
    }
}