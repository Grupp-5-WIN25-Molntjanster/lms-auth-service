using Lms.Auth.Application.DTOs;
using Lms.Auth.Application.Interfaces;
using Lms.Auth.Domain.Entities;
using Lms.Auth.Domain.Interfaces;

namespace Lms.Auth.Application.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IApplicationDbContext _context;
    private readonly IVerificationClient _verificationClient;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IApplicationDbContext context,
        IVerificationClient verificationClient)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
        _verificationClient = verificationClient;
    }

    // ================================================================
    // REGISTRATION – Send email to Service Bus, do NOT generate code
    // ================================================================

    public async Task<TokenResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await _userRepository.EmailExistsAsync(request.Email))
            return null;

        var passwordHash = _passwordHasher.Hash(request.Password);
        var role = string.IsNullOrEmpty(request.Role) ? "Student" : request.Role;
        var user = new User(request.Email, passwordHash, request.FirstName, request.LastName, role);

        // ============================================================
        // Auth does NOT generate or store codes.
        // Verification owns the code lifecycle. We just trigger a send.
        // ============================================================

        _userRepository.Add(user);
        await _context.SaveChangesAsync();

        // Trigger verification via Verification service (HTTP).
        // Wrapped so a transient Verification outage doesn't lose the new user —
        // the account exists and the user can request a resend.
        try
        {
            await _verificationClient.SendVerificationAsync(user.Email);
        }
        catch
        {
            // TODO: log — user can hit /resend-verification
        }

        return new TokenResponse
        {
            AccessToken = "",
            RefreshToken = "",
            ExpiresAt = DateTime.UtcNow,
            User = MapToUserResponse(user),
            RequiresEmailVerification = true
        };
    }

    // ================================================================
    // VERIFY EMAIL – User enters code, Auth Service verifies it
    // ================================================================

    /// <summary>
    /// Verifies the email using the code the user received.
    /// Auth delegates the actual code check to the Verification service.
    /// </summary>
    public async Task<bool> VerifyEmailAsync(string email, string code)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
            return false;

        if (user.EmailConfirmed)
            return true;

        var isValid = await _verificationClient.ValidateCodeAsync(email, code);

        if (isValid)
        {
            user.ConfirmEmail();
            _userRepository.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task<bool> ResendVerificationCodeAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
            return false;

        if (user.EmailConfirmed)
            return false;

        // Trigger another verification send via Verification service
        await _verificationClient.SendVerificationAsync(user.Email);
        return true;
    }

    public async Task<TokenResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        if (!user.IsActive)
            return null;

        // ============================================================
        // CHECK: Is email confirmed?
        // ============================================================
        if (!user.EmailConfirmed)
        {
            return new TokenResponse
            {
                AccessToken = "",
                RefreshToken = "",
                ExpiresAt = DateTime.UtcNow,
                User = MapToUserResponse(user),
                RequiresEmailVerification = true
            };
        }

        user.RecordLogin();
        _userRepository.Update(user);
        await _context.SaveChangesAsync();

        return await GenerateTokensAsync(user);
    }

    public async Task<TokenResponse?> RefreshTokenAsync(string refreshTokenValue)
    {
        var oldToken = await _refreshTokenRepository.GetByTokenAsync(refreshTokenValue);
        if (oldToken == null || oldToken.IsRevoked || oldToken.IsExpired)
            return null;

        var user = oldToken.User;
        if (!user.IsActive)
            return null;

        oldToken.Revoke();
        await _context.SaveChangesAsync();

        return await GenerateTokensAsync(user);
    }

    public async Task LogoutAsync(string refreshTokenValue)
    {
        var token = await _refreshTokenRepository.GetByTokenAsync(refreshTokenValue);
        if (token != null)
        {
            token.Revoke();
            await _context.SaveChangesAsync();
        }
    }

    public async Task<UserResponse?> ValidateTokenAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || !user.IsActive)
            return null;

        return MapToUserResponse(user);
    }

    private async Task<TokenResponse> GenerateTokensAsync(User user)
    {
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);
        var refreshTokenValue = _jwtTokenGenerator.GenerateRefreshToken();
        var refreshToken = new RefreshToken(
            refreshTokenValue,
            DateTime.UtcNow.AddDays(_jwtTokenGenerator.RefreshTokenExpirationDays),
            user);

        _refreshTokenRepository.Add(refreshToken);
        await _context.SaveChangesAsync();

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtTokenGenerator.AccessTokenExpirationMinutes),
            User = MapToUserResponse(user)
        };
    }

    /// <summary>
    /// Changes user's password after validating current password
    /// </summary>
    public async Task<ChangePasswordResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var response = new ChangePasswordResponse();

        // 1. Validate input
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            response.Success = false;
            response.Message = "Current password is required.";
            response.Errors.Add("current_password_required");
            return response;
        }

        // 2. Validate new password meets requirements
        if (!PasswordRequirements.IsValid(request.NewPassword, out var passwordErrors))
        {
            response.Success = false;
            response.Message = "New password does not meet security requirements.";
            response.Errors.AddRange(passwordErrors);
            return response;
        }

        // 3. Check if new password matches confirmation
        if (request.NewPassword != request.ConfirmNewPassword)
        {
            response.Success = false;
            response.Message = "New password and confirmation do not match.";
            response.Errors.Add("password_mismatch");
            return response;
        }

        // 4. Prevent using the same password
        if (request.CurrentPassword == request.NewPassword)
        {
            response.Success = false;
            response.Message = "New password cannot be the same as current password.";
            response.Errors.Add("same_password");
            return response;
        }

        // 5. Get user
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            response.Success = false;
            response.Message = "User not found.";
            response.Errors.Add("user_not_found");
            return response;
        }

        // 6. Verify current password
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            response.Success = false;
            response.Message = "Current password is incorrect.";
            response.Errors.Add("invalid_current_password");
            return response;
        }

        // 7. Check if account is active
        if (!user.IsActive)
        {
            response.Success = false;
            response.Message = "Account is deactivated. Cannot change password.";
            response.Errors.Add("account_inactive");
            return response;
        }

        // 8. Hash new password and update
        var newPasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatePassword(newPasswordHash);

        // 10. Save changes
        _userRepository.Update(user);
        await _context.SaveChangesAsync();

        response.Success = true;
        response.Message = "Password changed successfully. Please log in again with your new password.";

        return response;
    }


    private static UserResponse MapToUserResponse(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}