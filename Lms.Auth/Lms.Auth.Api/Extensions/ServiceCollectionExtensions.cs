using Lms.Auth.Application.Interfaces;
using Lms.Auth.Application.Services;
using Lms.Auth.Domain.Interfaces;
using Lms.Auth.Infrastructure.Options;
using Lms.Auth.Infrastructure.Persistence;
using Lms.Auth.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Lms.Auth.Api.Extensions;

/// <summary>
/// Extension methods for clean Program.cs.
/// Industrial standard: Keep Program.cs lean.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Options pattern with validation
        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Database
        services.AddDbContext<AuthDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("AuthDb")));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();

        // Infrastructure services
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }

    public static IServiceCollection AddAuthApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        return services;
    }
}