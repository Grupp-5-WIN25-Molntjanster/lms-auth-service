using Lms.Auth.Application.Interfaces;
using Lms.Auth.Application.Services;
using Lms.Auth.Domain.Interfaces;
using Lms.Auth.Infrastructure.Clients;
using Lms.Auth.Infrastructure.Persistence;
using Lms.Auth.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lms.Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AuthDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("AuthDb")));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AuthDbContext>());

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Security
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        // ============================================================
        // Verification Service Client
        // ============================================================
        var verificationUrl = configuration["ServiceUrls:VerificationService"];

        if (!string.IsNullOrWhiteSpace(verificationUrl))
        {
            // Real HTTP client when URL is configured
            services.AddHttpClient<IVerificationClient, VerificationClient>(client =>
            {
                client.BaseAddress = new Uri(verificationUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });
        }
        else
        {
            // No URL configured – register a dummy client that does nothing
            // This allows the app to run without the Verification Service
            services.AddSingleton<IVerificationClient, NoOpVerificationClient>();
        }

        // Application Services
        services.AddScoped<AuthService>();

        return services;
    }
}