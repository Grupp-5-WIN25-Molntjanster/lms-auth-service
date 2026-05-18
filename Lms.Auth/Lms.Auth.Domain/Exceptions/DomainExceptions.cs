namespace Lms.Auth.Domain.Exceptions;

/// <summary>
/// Base exception for all domain-level errors in the Auth service.
/// 
/// Domain exceptions represent business rule violations, not technical errors.
/// They should be handled gracefully and returned as 400 Bad Request responses.
/// This distinguishes them from infrastructure exceptions (500 Internal Server Error).
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    public DomainException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message explaining the domain rule violation.</param>
    public DomainException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception that caused this error.</param>
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when authentication or authorization fails.
/// Maps to HTTP 401 Unauthorized.
/// </summary>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message = "Invalid credentials.") : base(message) { }
}

/// <summary>
/// Exception thrown when input validation fails.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class ValidationException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}