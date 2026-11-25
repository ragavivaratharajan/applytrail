using ApplyTrail.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ApplyTrail.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<ReminderService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<ExportService>();
        return services;
    }
}
