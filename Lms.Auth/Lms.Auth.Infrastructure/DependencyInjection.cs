using Lms.Auth.Infrastructure;
using Lms.Auth.Application.Interfaces;
using Lms.Auth.Application.Services;
using Lms.Auth.Domain.Interfaces;
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
        // 1. Database
        services.AddDbContext<AuthDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("AuthDb")));

        // Register the interface so application layer can use IApplicationDbContext
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AuthDbContext>());

        // 2. Repositories (generic + specific)
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // 3. Security
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        // 4. Application Services
        services.AddScoped<AuthService>();

        return services;
    }
}