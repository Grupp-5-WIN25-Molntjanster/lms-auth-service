using System.ComponentModel.DataAnnotations;

namespace Lms.Auth.Infrastructure.Options;

/// <summary>
/// Strongly-typed JWT configuration using Options pattern.
/// Validated at startup to fail fast if misconfigured.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Secret { get; set; } = null!;

    [Required]
    public string Issuer { get; set; } = null!;

    [Required]
    public string Audience { get; set; } = null!;

    [Range(1, 1440)]
    public int AccessTokenExpiryMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenValidityDays { get; set; } = 7;
}