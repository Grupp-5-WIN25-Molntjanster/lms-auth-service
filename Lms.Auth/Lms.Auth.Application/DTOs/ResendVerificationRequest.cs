using System.ComponentModel.DataAnnotations;

namespace Lms.Auth.Application.DTOs;

public class ResendVerificationRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}