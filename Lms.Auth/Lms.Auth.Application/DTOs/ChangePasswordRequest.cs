namespace Lms.Auth.Application.DTOs;

public class ChangePasswordRequest
{
    /// <summary>
    /// Current password (required to verify identity)
    /// </summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// New password (must meet complexity requirements)
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// Confirm new password (must match NewPassword)
    /// </summary>
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
