using FluentAssertions;
using Lms.Auth.Application.DTOs;
using Lms.Auth.Application.Interfaces;
using Lms.Auth.Application.Services;
using Lms.Auth.Domain.Entities;
using Lms.Auth.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Timers;

namespace Lms.Auth.UnitTests.Application.Services;

/// <summary>
/// Unit tests for AuthService business logic.
/// 
/// These tests verify the CORE BUSINESS RULES without a real database:
/// - Registration validates duplicate emails
/// - Login verifies passwords correctly
/// - Refresh tokens are rotated (old one revoked)
/// - Inactive users cannot log in
/// 
/// All external dependencies (repositories, password hasher, JWT generator,
/// database context) are MOCKED using Moq. This makes tests FAST and RELIABLE.
/// </summary>
public class AuthServiceTests
{
    // ================================================================
    // Mock Dependencies (Fakes that we control)
    // ================================================================
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock;
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IServiceBusPublisher> _serviceBusPublisherMock;

    // ================================================================
    // System Under Test (The real AuthService, but with mocked dependencies)
    // ================================================================
    private readonly AuthService _authService;

    /// <summary>
    /// Constructor runs before EVERY test.
    /// Creates fresh mocks for each test to prevent test interference.
    /// </summary>
    public AuthServiceTests()
    {
        // Create mocks (fake objects that simulate real dependencies)
        _userRepositoryMock = new Mock<IUserRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _jwtTokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        _contextMock = new Mock<IApplicationDbContext>();
        _serviceBusPublisherMock = new Mock<IServiceBusPublisher>();

        // Setup JWT token generator defaults (can be overridden per test)
        _jwtTokenGeneratorMock.Setup(j => j.AccessTokenExpirationMinutes).Returns(15);
        _jwtTokenGeneratorMock.Setup(j => j.RefreshTokenExpirationDays).Returns(7);

        // Create the AuthService with mocked dependencies
        _authService = new AuthService(
            _userRepositoryMock.Object,           // Fake user repository
            _refreshTokenRepositoryMock.Object,   // Fake token repository
            _passwordHasherMock.Object,           // Fake password hasher
            _jwtTokenGeneratorMock.Object,        // Fake JWT generator
            _contextMock.Object,                // Fake database context
            _serviceBusPublisherMock.Object     // Fake service bus publisher
        );
    }

    // ================================================================
    // REGISTRATION TESTS
    // ================================================================

    /// <summary>
    /// TEST: Register a new user with valid data should succeed.
    /// 
    /// WHAT WE VERIFY:
    /// - Returns a TokenResponse (not null)
    /// - Access token is the expected value
    /// - User email is correct in the response
    /// - Repository.Add() was called exactly once
    /// - Database.SaveChangesAsync() was called at least once
    /// </summary>
    [Fact]
    public async Task RegisterAsync_WithValidData_ShouldReturnTokens()
    {
        // ============================================================
        // ARRANGE: Set up the test scenario
        // ============================================================
        var request = new RegisterRequest
        {
            Email = "newuser@lms.com",
            Password = "StrongPass123!",
            FirstName = "Jane",
            LastName = "Smith"
        };

        // Setup: Email does NOT exist yet (new user)
        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.Email))
            .ReturnsAsync(false);  // ← Email is available

        // Setup: Password hasher returns a fake hash
        _passwordHasherMock
            .Setup(p => p.Hash(request.Password))
            .Returns("hashed_password_xyz");

        // Setup: JWT generator returns predefined tokens
        _jwtTokenGeneratorMock
            .Setup(j => j.GenerateAccessToken(It.IsAny<User>()))
            .Returns("fake_access_token");
        _jwtTokenGeneratorMock
            .Setup(j => j.GenerateRefreshToken())
            .Returns("fake_refresh_token");

        // Setup: SaveChangesAsync returns 1 (1 row affected)
        _contextMock
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // ============================================================
        // ACT: Execute the method we're testing
        // ============================================================
        var result = await _authService.RegisterAsync(request);

        // ============================================================
        // ASSERT: Verify the results
        // ============================================================
        result.Should().NotBeNull("registration should succeed with valid data");
        result!.AccessToken.Should().Be("fake_access_token");
        result.RefreshToken.Should().Be("fake_refresh_token");
        result.User.Email.Should().Be("newuser@lms.com");
        result.User.Role.Should().Be("Student", "default role should be Student");

        // Verify repository was called to add the user
        _userRepositoryMock.Verify(
            r => r.Add(It.IsAny<User>()),
            Times.Once,
            "a new user should be added exactly once");

        // Verify changes were saved to the database
        _contextMock.Verify(
            c => c.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "changes should be saved to the database");
    }

    /// <summary>
    /// TEST: Register with an email that already exists should return null.
    /// 
    /// BUSINESS RULE: No duplicate emails allowed.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ShouldReturnNull()
    {
        // ARRANGE
        var request = new RegisterRequest
        {
            Email = "existing@lms.com",
            Password = "StrongPass123!",
            FirstName = "Jane",
            LastName = "Smith"
        };

        // Setup: Email ALREADY exists
        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.Email))
            .ReturnsAsync(true);  // ← Email is taken

        // ACT
        var result = await _authService.RegisterAsync(request);

        // ASSERT
        result.Should().BeNull("duplicate email should cause registration to fail");

        // Verify Add() was NEVER called (no user should be created)
        _userRepositoryMock.Verify(
            r => r.Add(It.IsAny<User>()),
            Times.Never,
            "no user should be added when email is duplicate");
    }

    // ================================================================
    // LOGIN TESTS
    // ================================================================

    /// <summary>
    /// TEST: Login with valid credentials should return tokens.
    /// 
    /// WHAT WE VERIFY:
    /// - Returns tokens for valid credentials
    /// - User's LastLoginAt is updated
    /// </summary>
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnTokens()
    {
        // ARRANGE
        var request = new LoginRequest
        {
            Email = "user@lms.com",
            Password = "correct_password"
        };

        var existingUser = new User(
            "user@lms.com",
            "hashed_password",
            "John",
            "Doe",
            "Student");

        // Setup: User exists in the database
        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(request.Email))
            .ReturnsAsync(existingUser);

        // Setup: Password is correct
        _passwordHasherMock
            .Setup(p => p.Verify(request.Password, existingUser.PasswordHash))
            .Returns(true);  // ← Password matches

        // Setup: JWT tokens
        _jwtTokenGeneratorMock
            .Setup(j => j.GenerateAccessToken(It.IsAny<User>()))
            .Returns("login_access_token");
        _jwtTokenGeneratorMock
            .Setup(j => j.GenerateRefreshToken())
            .Returns("login_refresh_token");

        _contextMock
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // ACT
        var result = await _authService.LoginAsync(request);

        // ASSERT
        result.Should().NotBeNull("valid credentials should succeed");
        result!.AccessToken.Should().Be("login_access_token");
        result.User.Email.Should().Be("user@lms.com");

        // Verify user was updated (LastLoginAt was set)
        _userRepositoryMock.Verify(
            r => r.Update(It.IsAny<User>()),
            Times.Once,
            "user should be updated with last login timestamp");
    }

    /// <summary>
    /// TEST: Login with wrong password should return null.
    /// 
    /// SECURITY: Never reveal whether the email or password was wrong.
    /// Always return the same error message.
    /// </summary>
    [Fact]
    public async Task LoginAsync_WithWrongPassword_ShouldReturnNull()
    {
        // ARRANGE
        var request = new LoginRequest
        {
            Email = "user@lms.com",
            Password = "wrong_password"
        };

        var existingUser = new User(
            "user@lms.com",
            "hashed_password",
            "John",
            "Doe",
            "Student");

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(request.Email))
            .ReturnsAsync(existingUser);

        // Setup: Password is WRONG
        _passwordHasherMock
            .Setup(p => p.Verify(request.Password, existingUser.PasswordHash))
            .Returns(false);  // ← Password does NOT match

        // ACT
        var result = await _authService.LoginAsync(request);

        // ASSERT
        result.Should().BeNull("wrong password should fail login");

        // Verify user was NOT updated (no login recorded)
        _userRepositoryMock.Verify(
            r => r.Update(It.IsAny<User>()),
            Times.Never,
            "user should not be updated on failed login");
    }

    /// <summary>
    /// TEST: Inactive user should not be able to log in.
    /// 
    /// BUSINESS RULE: Only active accounts can authenticate.
    /// </summary>
    [Fact]
    public async Task LoginAsync_WithInactiveUser_ShouldReturnNull()
    {
        // ARRANGE
        var request = new LoginRequest
        {
            Email = "inactive@lms.com",
            Password = "password"
        };

        // Create user using reflection to set IsActive to false
        // (because the domain model doesn't expose a setter for IsActive)
        var inactiveUser = new User(
            "inactive@lms.com",
            "hashed_password",
            "John",
            "Doe",
            "Student");

        // Use reflection to set IsActive = false (simulating a deactivated account)
        typeof(User).GetProperty("IsActive")?.SetValue(inactiveUser, false);

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(request.Email))
            .ReturnsAsync(inactiveUser);

        _passwordHasherMock
            .Setup(p => p.Verify(request.Password, inactiveUser.PasswordHash))
            .Returns(true);  // Password is correct, but user is inactive

        // ACT
        var result = await _authService.LoginAsync(request);

        // ASSERT
        result.Should().BeNull("inactive users should not be able to log in");
    }

    // ================================================================
    // REFRESH TOKEN TESTS
    // ================================================================

    /// <summary>
    /// TEST: Valid refresh token should return new tokens (rotation).
    /// 
    /// SECURITY PATTERN: Refresh Token Rotation
    /// - Old refresh token is revoked
    /// - New refresh token is issued
    /// - Prevents replay attacks
    /// </summary>
    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_ShouldRotateTokens()
    {
        // ARRANGE
        var oldRefreshTokenValue = "old_refresh_token";

        var user = new User(
            "user@lms.com",
            "hashed_password",
            "John",
            "Doe",
            "Student");

        var oldToken = new RefreshToken(
            oldRefreshTokenValue,
            DateTime.UtcNow.AddDays(1),  // Not expired
            user);

        // Setup: Old token exists and is valid
        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenAsync(oldRefreshTokenValue))
            .ReturnsAsync(oldToken);

        // Setup: New tokens
        _jwtTokenGeneratorMock
            .Setup(j => j.GenerateAccessToken(It.IsAny<User>()))
            .Returns("new_access_token");
        _jwtTokenGeneratorMock
            .Setup(j => j.GenerateRefreshToken())
            .Returns("new_refresh_token");

        _contextMock
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // ACT
        var result = await _authService.RefreshTokenAsync(oldRefreshTokenValue);

        // ASSERT
        result.Should().NotBeNull("valid refresh token should succeed");
        result!.AccessToken.Should().Be("new_access_token");
        result.RefreshToken.Should().Be("new_refresh_token");

        // Verify old token was revoked (rotation happened)
        oldToken.IsRevoked.Should().BeTrue("old token should be revoked during rotation");
    }

    /// <summary>
    /// TEST: Expired refresh token should return null.
    /// 
    /// BUSINESS RULE: Expired tokens cannot be used to get new access tokens.
    /// </summary>
    [Fact]
    public async Task RefreshTokenAsync_WithExpiredToken_ShouldReturnNull()
    {
        // ARRANGE
        var user = new User("user@lms.com", "hash", "John", "Doe", "Student");

        var expiredToken = new RefreshToken(
            "expired_token",
            DateTime.UtcNow.AddDays(-1),  // ← Expired yesterday
            user);

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenAsync("expired_token"))
            .ReturnsAsync(expiredToken);

        // ACT
        var result = await _authService.RefreshTokenAsync("expired_token");

        // ASSERT
        result.Should().BeNull("expired refresh token should be rejected");
    }

    // ================================================================
    // VALIDATE TOKEN TEST
    // ================================================================

    /// <summary>
    /// TEST: Validate should return user info for active users.
    /// </summary>
    [Fact]
    public async Task ValidateTokenAsync_WithActiveUser_ShouldReturnUserResponse()
    {
        // ARRANGE
        var user = new User(
            "user@lms.com",
            "hashed_password",
            "John",
            "Doe",
            "Instructor");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(user);

        // ACT
        var result = await _authService.ValidateTokenAsync(1);

        // ASSERT
        result.Should().NotBeNull();
        result!.Email.Should().Be("user@lms.com");
        result.Role.Should().Be("Instructor");
        result.IsActive.Should().BeTrue();
    }
}