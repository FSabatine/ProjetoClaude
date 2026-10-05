using System.Globalization;
using Fleet.Application.Analytics;
using Fleet.Application.Assignments;
using Fleet.Application.Assistant;
using Fleet.Application.Audit;
using Fleet.Application.Auth;
using Fleet.Application.Checklists;
using Fleet.Application.Documents;
using Fleet.Application.Files;
using Fleet.Application.Finance;
using Fleet.Application.Fuel;
using Fleet.Application.Mileage;
using Fleet.Application.Occurrences;
using Fleet.Application.Operations;
using Fleet.Application.Companies;
using Fleet.Application.Dashboard;
using Fleet.Application.Drivers;
using Fleet.Application.Implements;
using Fleet.Application.Intelligence;
using Fleet.Application.Maintenance;
using Fleet.Application.Roles;
using Fleet.Application.Tires;
using Fleet.Application.Users;
using Fleet.Application.Vehicles;
using Fleet.Application.Common;
using Fleet.Domain.Intelligence;
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

        // Phase 3 — maintenance
        services.AddScoped<WorkshopService>();
        services.AddScoped<MaintenancePlanService>();
        services.AddScoped<MaintenanceScheduleService>();
        services.AddScoped<HourMeterService>();
        services.AddScoped<WorkOrderService>();
        services.AddScoped<MaintenanceRequestService>();

        // Phase 4 — fuel
        services.AddScoped<FuelTypeService>();
        services.AddScoped<FuelStationService>();
        services.AddScoped<FuelSettingsService>();
        services.AddScoped<FuelConsumptionService>();
        services.AddScoped<FuelingService>();
        services.AddScoped<FuelAnalyticsService>();

        // Phase 5 — tires
        services.AddScoped<TireModelService>();
        services.AddScoped<TireLayoutService>();
        services.AddScoped<TireSettingsService>();
        services.AddScoped<TireLifecycle>();
        services.AddScoped<TireMonitoring>();
        services.AddScoped<TireService>();
        services.AddScoped<TireOperationsService>();
        services.AddScoped<TireInspectionService>();
        services.AddScoped<TireServiceOrderService>();
        services.AddScoped<TireAnalyticsService>();

        // Phase 6 — finance
        services.AddScoped<CostCenterService>();
        services.AddScoped<ExpenseCategoryService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<RecurringExpenseService>();
        services.AddScoped<RecurringExpenseGenerationScanner>();
        services.AddScoped<BudgetService>();
        services.AddScoped<CostAggregationService>();
        services.AddScoped<FinanceAnalyticsService>();

        // Final phase — alerts and automation (ADR-045). One detector per scheduled trigger.
        services.AddScoped<SystemExecutionContext>();
        services.AddScoped<IAlertDetector>(sp => new MaintenanceDueDetector(
            sp.GetRequiredService<IFleetDbContext>(), sp.GetRequiredService<IClock>(), sp.GetRequiredService<MaintenanceScheduleService>(),
            AutomationTrigger.MaintenanceOverdue));
        services.AddScoped<IAlertDetector>(sp => new MaintenanceDueDetector(
            sp.GetRequiredService<IFleetDbContext>(), sp.GetRequiredService<IClock>(), sp.GetRequiredService<MaintenanceScheduleService>(),
            AutomationTrigger.MaintenanceDueSoon));
        services.AddScoped<IAlertDetector, FuelConsumptionDetector>();
        services.AddScoped<IAlertDetector, VehicleCostDetector>();
        services.AddScoped<IAlertDetector, BudgetThresholdDetector>();
        services.AddScoped<IAlertDetector, TireTreadDetector>();
        services.AddScoped<IAlertDetector, ExpenseOverdueDetector>();
        services.AddScoped<IAlertDetector, DocumentExpiringDetector>();
        services.AddScoped<AutomationRuleService>();
        services.AddScoped<AutomationEngine>();
        services.AddScoped<FleetAlertService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<AttentionService>();

        // Final phase — cross-module analytics (reports, comparison, health, insights, search)
        services.AddScoped<VehicleMetricsService>();
        services.AddScoped<FleetReportsService>();
        services.AddScoped<VehicleHealthService>();
        services.AddScoped<InsightService>();
        services.AddScoped<GlobalSearchService>();

        // Final phase — assistant (ADR-050). IAssistantLanguageModel is provided by Infrastructure (Claude, optional).
        services.AddScoped<AssistantToolbox>();
        services.AddScoped<AssistantService>();
        return services;
    }
}
