namespace Lms.Auth.Application.Interfaces;

/// <summary>
/// Publishes messages to Azure Service Bus.
/// Used for verification emails (consumed by Email Service).
/// </summary>
public interface IServiceBusPublisher
{
    Task PublishVerificationEmailAsync(VerificationMessage message);
}

public class VerificationMessage
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string MessageType { get; set; } = "EmailVerification";
}