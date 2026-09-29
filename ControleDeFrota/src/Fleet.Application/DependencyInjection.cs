using System.Globalization;
using Fleet.Application.Audit;
using Fleet.Application.Auth;
using Fleet.Application.Companies;
using Fleet.Application.Dashboard;
using Fleet.Application.Drivers;
using Fleet.Application.Implements;
using Fleet.Application.Roles;
using Fleet.Application.Users;
using Fleet.Application.Vehicles;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Application;

public static class DependencyInjection
{
    /// <summary>Every new module service is registered here.</summary>
    public static IServiceCollection AddFleetApplication(this IServiceCollection services)
    {
        // One message per field (the first failing rule) and pt-BR built-in messages.
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<RoleService>();
        services.AddScoped<CompanyService>();
        services.AddScoped<DriverService>();
        services.AddScoped<VehicleService>();
        services.AddScoped<ImplementService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<AuditService>();
        return services;
    }
}
