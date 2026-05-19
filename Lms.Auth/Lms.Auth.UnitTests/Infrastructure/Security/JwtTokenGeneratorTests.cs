using FluentAssertions;
using Lms.Auth.Domain.Entities;
using Lms.Auth.Infrastructure.Security;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

namespace Lms.Auth.UnitTests.Infrastructure.Security;

/// <summary>
/// Unit tests for JWT token generation.
/// 
/// These tests verify:
/// - Access tokens contain the correct claims (sub, email, role)
/// - Access token expiration is correctly set
/// - Refresh tokens are unique and long enough
/// - The JWT is valid and can be read back
/// 
/// JWT (JSON Web Token) is a standard for securely transmitting
/// information between parties as a JSON object.
/// </summary>
public class JwtTokenGeneratorTests
{
    private readonly JwtTokenGenerator _jwtTokenGenerator;
    private readonly User _testUser;

    /// <summary>
    /// Creates a real JwtTokenGenerator with a test secret key.
    /// Uses IOptions<T> pattern (same as production DI).
    /// </summary>
    public JwtTokenGeneratorTests()
    {
        var settings = new JwtSettings
        {
            Secret = "test-secret-key-that-is-at-least-32-characters-long!!",
            Issuer = "lms-auth-service",
            Audience = "lms-api",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };

        var options = Options.Create(settings);
        _jwtTokenGenerator = new JwtTokenGenerator(options);

        _testUser = new User(
            "test@lms.com",
            "hashed_password",
            "John",
            "Doe",
            "Student");
    }

    /// <summary>
    /// TEST: GenerateAccessToken should produce a valid JWT string.
    /// 
    /// A valid JWT has three parts separated by dots:
    /// header.payload.signature
    /// </summary>
    [Fact]
    public void GenerateAccessToken_ShouldReturnValidJwt()
    {
        // ACT
        var token = _jwtTokenGenerator.GenerateAccessToken(_testUser);

        // ASSERT
        token.Should().NotBeNullOrEmpty("JWT should not be empty");
        token.Split('.').Should().HaveCount(3,
            "JWT consists of 3 parts: header, payload, signature");
    }

    /// <summary>
    /// TEST: The access token should contain the user's claims.
    /// 
    /// We read the JWT back using JwtSecurityTokenHandler to verify
    /// the claims were correctly embedded.
    /// </summary>
    [Fact]
    public void GenerateAccessToken_ShouldContainUserClaims()
    {
        // ACT
        var token = _jwtTokenGenerator.GenerateAccessToken(_testUser);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // ASSERT
        jwtToken.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.Sub &&
            c.Value == _testUser.Id.ToString(),
            "token should contain user ID as 'sub' claim");

        jwtToken.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.Email &&
            c.Value == "test@lms.com",
            "token should contain user email");

        jwtToken.Claims.Should().Contain(c =>
            c.Type == "role" &&
            c.Value == "Student",
            "token should contain user role");
    }

    /// <summary>
    /// TEST: The access token should expire after 15 minutes.
    /// 
    /// We verify that ValidTo is set correctly in the future.
    /// </summary>
    [Fact]
    public void GenerateAccessToken_ShouldSetCorrectExpiration()
    {
        // ACT
        var token = _jwtTokenGenerator.GenerateAccessToken(_testUser);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // ASSERT
        jwtToken.ValidTo.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(15),
            TimeSpan.FromMinutes(1),
            "token should expire approximately 15 minutes from now");
    }

    /// <summary>
    /// TEST: GenerateRefreshToken should return a unique, long string.
    /// 
    /// Refresh tokens are random strings (not JWTs) stored in the database.
    /// They are 64 bytes encoded as Base64, resulting in ~88 characters.
    /// </summary>
    [Fact]
    public void GenerateRefreshToken_ShouldReturnLongRandomString()
    {
        // ACT
        var token = _jwtTokenGenerator.GenerateRefreshToken();

        // ASSERT
        token.Should().NotBeNullOrEmpty();
        token.Length.Should().BeGreaterThan(50,
            "refresh token should be long enough for security");

        // Generate another one – should be different (random)
        var token2 = _jwtTokenGenerator.GenerateRefreshToken();
        token.Should().NotBe(token2,
            "each refresh token should be unique");
    }

    /// <summary>
    /// TEST: Token issued by our generator should have the correct issuer.
    /// </summary>
    [Fact]
    public void GenerateAccessToken_ShouldHaveCorrectIssuer()
    {
        // ACT
        var token = _jwtTokenGenerator.GenerateAccessToken(_testUser);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // ASSERT
        jwtToken.Issuer.Should().Be("lms-auth-service",
            "issuer should match the configured value");
    }

    /// <summary>
    /// TEST: AccessTokenExpirationMinutes should return the configured value.
    /// </summary>
    [Fact]
    public void AccessTokenExpirationMinutes_ShouldReturnConfiguredValue()
    {
        // ACT
        var minutes = _jwtTokenGenerator.AccessTokenExpirationMinutes;

        // ASSERT
        minutes.Should().Be(15);
    }

    /// <summary>
    /// TEST: RefreshTokenExpirationDays should return the configured value.
    /// </summary>
    [Fact]
    public void RefreshTokenExpirationDays_ShouldReturnConfiguredValue()
    {
        // ACT
        var days = _jwtTokenGenerator.RefreshTokenExpirationDays;

        // ASSERT
        days.Should().Be(7);
    }
}