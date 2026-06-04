namespace Lms.Auth.Application.Interfaces;

/// <summary>
/// Talks to the Verification service over HTTP.
/// Verification owns the full code lifecycle: generate, store, publish, validate.
/// Auth only triggers a send and asks whether a submitted code is valid.
/// </summary>
public interface IVerificationClient
{
    /// <summary>Asks Verification to generate, store and dispatch a code for this email.</summary>
    Task SendVerificationAsync(string email);

    /// <summary>Asks Verification whether the submitted code is valid for this email.</summary>
    Task<bool> ValidateCodeAsync(string email, string code);
}