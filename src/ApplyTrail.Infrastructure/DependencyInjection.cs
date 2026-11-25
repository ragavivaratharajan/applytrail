using ApplyTrail.Application.Abstractions;
using ApplyTrail.Infrastructure.Auth;
using ApplyTrail.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApplyTrail.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Everything that reads configuration does so lazily, from the built service provider. That way the final,
    /// fully merged configuration is used (including overrides applied late, such as in integration tests).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            options.UseSqlite(configuration.GetConnectionString("Default") ?? "Data Source=applytrail.db");
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton(sp => JwtOptions.Resolve(
            sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<IHostEnvironment>()));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            return new AuthSettings(
                AllowRegistration: configuration.GetValue("Auth:AllowRegistration", true),
                CookieSecure: configuration.GetValue("Auth:CookieSecure", true));
        });

        return services;
    }
}
