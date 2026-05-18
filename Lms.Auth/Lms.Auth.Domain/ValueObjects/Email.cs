using System.Text.RegularExpressions;

namespace Lms.Auth.Domain.ValueObjects;

public sealed class Email : IEquatable<Email>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.");

        var trimmed = email.Trim().ToLowerInvariant();

        if (trimmed.Length > 256)
            throw new ArgumentException("Email cannot exceed 256 characters.");

        if (!EmailRegex.IsMatch(trimmed))
            throw new ArgumentException("Email format is invalid.");

        return new Email(trimmed);
    }

    public override string ToString() => Value;

    public bool Equals(Email? other)
    {
        if (other is null) return false;
        return Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
        => obj is Email email && Equals(email);

    public override int GetHashCode()
        => Value.GetHashCode(StringComparison.OrdinalIgnoreCase);

    public static bool operator ==(Email? left, Email? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(Email? left, Email? right)
        => !(left == right);
}