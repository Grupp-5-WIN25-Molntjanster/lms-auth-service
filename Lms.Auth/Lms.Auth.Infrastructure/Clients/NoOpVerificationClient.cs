using Lms.Auth.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Lms.Auth.Infrastructure.Clients;

/// <summary>
/// No-op implementation of IVerificationClient.
/// Used when the Verification Service URL is not configured.
/// All methods return success so the app functions without the service.
/// </summary>
public class NoOpVerificationClient : IVerificationClient
{
    private readonly ILogger<NoOpVerificationClient> _logger;

    public NoOpVerificationClient(ILogger<NoOpVerificationClient> logger)
    {
        _logger = logger;
    }

    public Task SendVerificationAsync(string email)
    {
        _logger.LogWarning(
            "Verification service is not configured. " +
            "Skipping verification email for {Email}. " +
            "Set 'ServiceUrls:VerificationService' in configuration.", email);
        return Task.CompletedTask;
    }

    public Task<bool> ValidateCodeAsync(string email, string code)
    {
        _logger.LogWarning(
            "Verification service is not configured. " +
            "Auto-accepting verification for {Email}. " +
            "Set 'ServiceUrls:VerificationService' in configuration.", email);
        return Task.FromResult(true); // Auto-accept when no service
    }
}