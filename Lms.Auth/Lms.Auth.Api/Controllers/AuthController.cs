using Lms.Auth.Application.DTOs;
using Lms.Auth.Application.Interfaces;
using Lms.Auth.Application.Services;
using Lms.Auth.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Lms.Auth.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly IUserRepository _userRepository;


    public AuthController(AuthService authService, IUserRepository userRepository)
    {
        _authService = authService;
        _userRepository = userRepository;
    }


    /// <summary>
    /// Registers a new user account. Anonymous.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _authService.RegisterAsync(request);
        if (result == null)
            return Conflict(new { error = "email_taken", message = "An account with this email already exists." });

        return CreatedAtAction(nameof(Validate), new { }, result);
    }

    /// <summary>
    /// Verify email with the code received from Email Service.
    /// </summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.VerifyEmailAsync(request.Email, request.Code);

        if (result)
            return Ok(new { message = "Email verified successfully. You can now log in." });

        return BadRequest(new
        {
            error = "verification_failed",
            message = "Invalid verification code. Please check your email and try again."
        });
    }

    /// <summary>
    /// Resend verification email. Delegates to the Verification service.
    /// Returns a generic response so it cannot be used to enumerate accounts.
    /// </summary>
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _authService.ResendVerificationCodeAsync(request.Email);

        return Ok(new { message = "If an account exists for this email, a verification email has been sent." });
    }



    /// <summary>
    /// Authenticates a user and returns JWT tokens. Anonymous.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _authService.LoginAsync(request);
        if (result == null)
            return Unauthorized(new { error = "invalid_credentials", message = "Invalid email or password." });

        return Ok(result);
    }

    /// <summary>
    /// Refreshes the access token using a valid refresh token. Anonymous.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return BadRequest(new { error = "invalid_request", message = "Refresh token is required." });

        var result = await _authService.RefreshTokenAsync(refreshToken);
        if (result == null)
            return Unauthorized(new { error = "invalid_refresh_token", message = "Invalid or expired refresh token." });

        return Ok(result);
    }

    /// <summary>
    /// Revokes the given refresh token, effectively logging the user out. Requires authentication.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] string refreshToken)
    {
        await _authService.LogoutAsync(refreshToken);
        return NoContent();
    }

    /// <summary>
    /// Changes user's password. Requires authentication.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        // Validate model
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Get user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "invalid_token", message = "User not authenticated." });

        var result = await _authService.ChangePasswordAsync(userId, request);

        if (!result.Success)
        {
            return BadRequest(new
            {
                error = "password_change_failed",
                message = result.Message,
                details = result.Errors
            });
        }

        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Validates the current JWT and returns the user profile. Requires authentication.
    /// </summary>
    [HttpGet("validate")]
    [Authorize]
    public async Task<IActionResult> Validate()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _authService.ValidateTokenAsync(userId);
        if (user == null) return Unauthorized();
        return Ok(user);
    }
}