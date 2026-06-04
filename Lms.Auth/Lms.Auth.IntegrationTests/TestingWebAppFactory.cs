using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Lms.Auth.Infrastructure.Persistence;
using Lms.Auth.Application.Interfaces;

namespace Lms.Auth.IntegrationTests
{
    /// <summary>
    /// Custom WebApplicationFactory that replaces the real SQL Server DbContext
    /// and Service Bus publisher with in-memory/test doubles for integration tests.
    /// </summary>
    public class TestingWebAppFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint> where TEntryPoint : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove all registrations that reference AuthDbContext or its options
                var descriptorsToRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AuthDbContext>) ||
                    d.ServiceType == typeof(AuthDbContext) ||
                    (d.ImplementationType != null && d.ImplementationType == typeof(AuthDbContext)) ||
                    d.ServiceType == typeof(Lms.Auth.Application.Interfaces.IApplicationDbContext)
                ).ToList();

                foreach (var d in descriptorsToRemove)
                    services.Remove(d);

                // Add InMemory DbContext with unique name per test run
                services.AddDbContext<AuthDbContext>(options =>
                    options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid():N}"));

                // Register IApplicationDbContext to resolve to AuthDbContext
                services.AddScoped<Lms.Auth.Application.Interfaces.IApplicationDbContext>(sp => sp.GetRequiredService<AuthDbContext>());

            });
        }
    }
}