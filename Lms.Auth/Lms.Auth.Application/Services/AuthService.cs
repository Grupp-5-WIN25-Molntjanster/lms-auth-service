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
    private readonly IServiceBusPublisher _serviceBusPublisher;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IApplicationDbContext context,
        IServiceBusPublisher serviceBusPublisher)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
        _serviceBusPublisher = serviceBusPublisher;
    }

    public async Task<TokenResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await _userRepository.EmailExistsAsync(request.Email))
            return null;

        var passwordHash = _passwordHasher.Hash(request.Password);
        var role = string.IsNullOrEmpty(request.Role) ? "Student" : request.Role;
        var user = new User(request.Email, passwordHash, request.FirstName, request.LastName, role);

        // ============================================================
        // Generate 6-digit verification code
        // ============================================================
        var random = new Random();
        var code = random.Next(100000, 999999).ToString();
        user.SetVerificationCode(code, DateTime.UtcNow.AddHours(1));

        _userRepository.Add(user);
        await _context.SaveChangesAsync();

        // ============================================================
        // Publish to Service Bus (Email Service picks this up)
        // ============================================================
        await _serviceBusPublisher.PublishVerificationEmailAsync(new VerificationMessage
        {
            UserId = user.Id,
            Email = user.Email,
            VerificationCode = code
        });

        // Return response WITHOUT tokens (must verify email first)
        return new TokenResponse
        {
            AccessToken = "",
            RefreshToken = "",
            ExpiresAt = DateTime.UtcNow,
            User = MapToUserResponse(user),
            RequiresEmailVerification = true
        };
    }

    // ============================================================
    // EMAIL VERIFICATION METHODS
    // ============================================================

    /// <summary>
    /// Verifies email with the code sent to user.
    /// Returns true if verification succeeded.
    /// </summary>
    public async Task<bool> VerifyEmailAsync(string email, string code)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
            return false;

        if (user.EmailConfirmed)
            return true; // Already verified

        if (user.IsVerificationCodeExpired)
            return false;

        if (user.VerificationCode != code)
            return false;

        user.ConfirmEmail();
        _userRepository.Update(user);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Generates a new verification code and publishes to Service Bus.
    /// Used when user requests a new code.
    /// </summary>
    public async Task<bool> ResendVerificationCodeAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
            return false;

        if (user.EmailConfirmed)
            return false; // Already verified, no need to resend

        var random = new Random();
        var code = random.Next(100000, 999999).ToString();
        user.SetVerificationCode(code, DateTime.UtcNow.AddHours(1));
        _userRepository.Update(user);
        await _context.SaveChangesAsync();

        await _serviceBusPublisher.PublishVerificationEmailAsync(new VerificationMessage
        {
            UserId = user.Id,
            Email = user.Email,
            VerificationCode = code
        });

        return true;
    }


    public async Task<TokenResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        if (!user.IsActive)
            return null;

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

    public async Task<UserResponse?> ValidateTokenAsync(int userId)
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
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}