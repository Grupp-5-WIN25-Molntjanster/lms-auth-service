using Lms.Auth.Domain.Entities;

namespace Lms.Auth.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    int AccessTokenExpirationMinutes { get; }
    int RefreshTokenExpirationDays { get; }
}