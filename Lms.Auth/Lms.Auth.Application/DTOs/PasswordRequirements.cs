namespace Lms.Auth.Application.DTOs;

public static class PasswordRequirements
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    public static readonly string Requirements =
        $"Password must be at least {MinimumLength} characters long and contain:\n" +
        "• At least one uppercase letter (A-Z)\n" +
        "• At least one lowercase letter (a-z)\n" +
        "• At least one digit (0-9)\n" +
        "• At least one special character (!@#$%^&* etc.)";

    public static bool IsValid(string password, out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password cannot be empty.");
            return false;
        }

        if (password.Length < MinimumLength)
            errors.Add($"Password must be at least {MinimumLength} characters long.");

        if (password.Length > MaximumLength)
            errors.Add($"Password cannot exceed {MaximumLength} characters.");

        if (!password.Any(char.IsUpper))
            errors.Add("Password must contain at least one uppercase letter.");

        if (!password.Any(char.IsLower))
            errors.Add("Password must contain at least one lowercase letter.");

        if (!password.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit.");

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            errors.Add("Password must contain at least one special character.");

        return errors.Count == 0;
    }
}