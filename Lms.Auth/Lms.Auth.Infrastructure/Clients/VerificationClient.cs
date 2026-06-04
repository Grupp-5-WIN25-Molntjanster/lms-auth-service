using System.Net.Http.Json;
using Lms.Auth.Application.Interfaces;

namespace Lms.Auth.Infrastructure.Clients;

public class VerificationClient : IVerificationClient
{
    private readonly HttpClient _http;

    public VerificationClient(HttpClient http) => _http = http;

    public async Task SendVerificationAsync(string email)
    {
        // Verification uses [FromBody] -> send JSON, not query string
        var response = await _http.PostAsJsonAsync(
            "api/Verification/send",
            new { email });

        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> ValidateCodeAsync(string email, string code)
    {
        // Verification's Code is an int in the body -> must send a number
        if (!int.TryParse(code, out var codeNumber))
            return false;

        var response = await _http.PostAsJsonAsync(
            "api/Verification/validate",
            new { email, code = codeNumber });

        // Verification returns 200 { message } on success, 400 { message } on failure.
        // There is no `valid` field, so we rely on the status code.
        return response.IsSuccessStatusCode;
    }
}