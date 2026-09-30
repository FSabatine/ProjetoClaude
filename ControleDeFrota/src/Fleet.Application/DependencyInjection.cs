using System.Globalization;
using Fleet.Application.Assignments;
using Fleet.Application.Audit;
using Fleet.Application.Auth;
using Fleet.Application.Checklists;
using Fleet.Application.Documents;
using Fleet.Application.Files;
using Fleet.Application.Mileage;
using Fleet.Application.Occurrences;
using Fleet.Application.Operations;
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

        // Phase 2 — operational control
        services.AddScoped<OperationalEventLog>();
        services.AddScoped<OperationalHistoryService>();
        services.AddScoped<AssignmentService>();
        services.AddScoped<MileageService>();
        services.AddScoped<FileService>();
        services.AddScoped<DocumentTypeService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<DocumentExpirationScanner>();
        services.AddScoped<ChecklistTemplateService>();
        services.AddScoped<ChecklistService>();
        services.AddScoped<OccurrenceService>();
        return services;
    }
}
