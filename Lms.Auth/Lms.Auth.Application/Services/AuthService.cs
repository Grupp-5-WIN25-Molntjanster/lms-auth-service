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

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IApplicationDbContext context)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
    }

    public async Task<TokenResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await _userRepository.EmailExistsAsync(request.Email))
            return null;

        var passwordHash = _passwordHasher.Hash(request.Password);
        var role = string.IsNullOrEmpty(request.Role) ? "Student" : request.Role;
        var user = new User(request.Email, passwordHash, request.FirstName, request.LastName, role);

        _userRepository.Add(user);
        await _context.SaveChangesAsync();

        return await GenerateTokensAsync(user);
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