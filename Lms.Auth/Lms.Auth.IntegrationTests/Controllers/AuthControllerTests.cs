using FluentAssertions;
using Lms.Auth.Application.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net.Http.Json;
using Xunit;

namespace Lms.Auth.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for the Auth API endpoints.
/// 
/// These tests spin up a REAL ASP.NET Core server in memory
/// using WebApplicationFactory. They test the FULL pipeline:
/// 
/// HTTP Request → Middleware → Controller → Service → Repository → Database
/// 
/// We use EF Core In-Memory database so tests are:
/// - Fast (no SQL Server needed)
/// - Isolated (each test gets a fresh database)
/// - Realistic (real HTTP calls, real JSON serialization)
/// 
/// This is the CLOSEST you can get to production testing
/// without actually deploying to Azure.
/// </summary>
public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Constructor receives a WebApplicationFactory that creates
    /// a test server with our real Program.cs configuration.
    /// </summary>
    public AuthControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// TEST: Register endpoint should return 201 Created with tokens.
    /// 
    /// END-TO-END FLOW TESTED:
    /// 1. HTTP POST to /api/auth/register
    /// 2. JSON deserialization
    /// 3. Model validation
    /// 4. Controller action
    /// 5. Application service
    /// 6. Repository
    /// 7. Database (in-memory)
    /// 8. JWT generation
    /// 9. HTTP response serialization
    /// </summary>
    [Fact]
    public async Task Register_WithValidData_ShouldReturnCreated()
    {
        // ARRANGE: Create a registration request
        var request = new RegisterRequest
        {
            Email = "integration-test@lms.com",
            Password = "IntegrationTest123!",
            FirstName = "Integration",
            LastName = "Test"
        };

        // ACT: Send real HTTP POST request
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // ASSERT: Verify HTTP status code
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created,
            "successful registration should return 201 Created");

        // ASSERT: Verify response body contains tokens
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokenResponse.Should().NotBeNull();
        tokenResponse!.AccessToken.Should().NotBeNullOrEmpty();
        tokenResponse.RefreshToken.Should().NotBeNullOrEmpty();
        tokenResponse.User.Email.Should().Be("integration-test@lms.com");
    }

    /// <summary>
    /// TEST: Register with duplicate email should return 409 Conflict.
    /// </summary>
    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnConflict()
    {
        // ARRANGE
        var request = new RegisterRequest
        {
            Email = "duplicate@lms.com",
            Password = "Test1234!",
            FirstName = "First",
            LastName = "User"
        };

        // Register once (should succeed)
        await _client.PostAsJsonAsync("/api/auth/register", request);

        // ACT: Register again with same email
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // ASSERT
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict,
            "duplicate email should return 409 Conflict");
    }

    /// <summary>
    /// TEST: Login with valid credentials should return 200 OK with tokens.
    /// </summary>
    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnOk()
    {
        // ARRANGE: First register a user
        var registerRequest = new RegisterRequest
        {
            Email = "login-test@lms.com",
            Password = "LoginTest123!",
            FirstName = "Login",
            LastName = "Test"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // ACT: Login with the same credentials
        var loginRequest = new LoginRequest
        {
            Email = "login-test@lms.com",
            Password = "LoginTest123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // ASSERT
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokenResponse.Should().NotBeNull();
        tokenResponse!.User.Email.Should().Be("login-test@lms.com");
    }

    /// <summary>
    /// TEST: Login with wrong password should return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task Login_WithWrongPassword_ShouldReturnUnauthorized()
    {
        // ARRANGE: Register a user
        var registerRequest = new RegisterRequest
        {
            Email = "wrong-pass@lms.com",
            Password = "CorrectPass123!",
            FirstName = "Wrong",
            LastName = "Pass"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // ACT: Try to login with WRONG password
        var loginRequest = new LoginRequest
        {
            Email = "wrong-pass@lms.com",
            Password = "WrongPassword!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // ASSERT
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized,
            "wrong password should return 401");
    }
}