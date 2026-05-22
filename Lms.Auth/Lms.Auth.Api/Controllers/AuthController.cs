using System.Security.Claims;
using Lms.Auth.Application.DTOs;
using Lms.Auth.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Auth.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService) => _authService = authService;

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
    /// Verify email address using the code sent via email.
    /// The Email Service (other student) sent the code.
    /// </summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            message = "Invalid or expired verification code. Please request a new one."
        });
    }

    /// <summary>
    /// Resend verification code to email.
    /// Called when code expires or user didn't receive it.
    /// </summary>
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.ResendVerificationCodeAsync(request.Email);

        if (result)
            return Ok(new { message = "Verification code sent. Please check your email." });

        return BadRequest(new
        {
            error = "resend_failed",
            message = "Unable to resend code. Email may already be verified or account not found."
        });
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
    /// Validates the current JWT and returns the user profile. Requires authentication.
    /// </summary>
    [HttpGet("validate")]
    [Authorize]
    public async Task<IActionResult> Validate()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _authService.ValidateTokenAsync(userId);
        if (user == null) return Unauthorized();
        return Ok(user);
    }
}