namespace Fleet.Domain.Authorization;

public sealed record RoleDefinition(int Id, string Key, string Name, string Description, IReadOnlyList<string> Permissions);

/// <summary>
/// System roles seeded into Roles/RolePermissions. This is the ONLY place where a role
/// maps to permissions; application code must never branch on a role key.
/// </summary>
public static class SystemRoles
{
    public const string PlatformAdministrator = "PlatformAdministrator";
    public const string Administrator = "Administrator";
    public const string FleetManager = "FleetManager";
    public const string Operations = "Operations";
    public const string Maintenance = "Maintenance";
    public const string Finance = "Finance";
    public const string Driver = "Driver";
    public const string Viewer = "Viewer";

    private static readonly string[] AllPermissions = PermissionCatalog.All.Select(p => p.Key).ToArray();

    private static readonly string[] FleetRegistryFull =
    [
        Permissions.Drivers.View, Permissions.Drivers.Create, Permissions.Drivers.Update, Permissions.Drivers.Delete,
        Permissions.Vehicles.View, Permissions.Vehicles.Create, Permissions.Vehicles.Update, Permissions.Vehicles.Delete,
        Permissions.Implements.View, Permissions.Implements.Create, Permissions.Implements.Update, Permissions.Implements.Delete,
    ];

    private static readonly string[] FleetRegistryRead =
        [Permissions.Drivers.View, Permissions.Vehicles.View, Permissions.Implements.View];

    private static readonly string[] OperationsFull =
    [
        Permissions.Assignments.View, Permissions.Assignments.Manage,
        Permissions.Mileage.Record, Permissions.Mileage.Manage,
        Permissions.Documents.View, Permissions.Documents.Manage, Permissions.Documents.Delete,
        Permissions.Checklists.View, Permissions.Checklists.Execute,
        Permissions.Occurrences.View, Permissions.Occurrences.Create, Permissions.Occurrences.Manage,
        Permissions.Operations.Configure,
    ];

    /// <summary>Day-to-day operation: create and edit, but no deletion, corrections or configuration.</summary>
    private static readonly string[] OperationsDaily =
    [
        Permissions.Assignments.View, Permissions.Assignments.Manage,
        Permissions.Mileage.Record,
        Permissions.Documents.View, Permissions.Documents.Manage,
        Permissions.Checklists.View, Permissions.Checklists.Execute,
        Permissions.Occurrences.View, Permissions.Occurrences.Create, Permissions.Occurrences.Manage,
    ];

    private static readonly string[] OperationsRead =
        [Permissions.Assignments.View, Permissions.Documents.View, Permissions.Checklists.View, Permissions.Occurrences.View];

    private static readonly string[] MaintenanceFull =
    [
        Permissions.Maintenance.View, Permissions.Maintenance.CreateRequest, Permissions.Maintenance.ManagePlans,
        Permissions.Maintenance.ManageWorkOrders, Permissions.Maintenance.ManageWorkshops, Permissions.Maintenance.ViewCosts,
    ];

    private static readonly string[] FuelFull =
    [
        Permissions.Fuel.View, Permissions.Fuel.Create, Permissions.Fuel.Correct, Permissions.Fuel.Cancel,
        Permissions.Fuel.ReviewAnomalies, Permissions.Fuel.ManageStations, Permissions.Fuel.Configure, Permissions.Fuel.ViewCosts,
    ];

    private static readonly string[] TiresFull =
    [
        Permissions.Tires.View, Permissions.Tires.Create, Permissions.Tires.Edit, Permissions.Tires.Install, Permissions.Tires.Remove,
        Permissions.Tires.Rotate, Permissions.Tires.Inspect, Permissions.Tires.Repair, Permissions.Tires.Retread,
        Permissions.Tires.Dispose, Permissions.Tires.ViewCosts, Permissions.Tires.ManageSettings,
    ];

    public static readonly IReadOnlyList<RoleDefinition> All =
    [
        new(1, PlatformAdministrator, "Administrador da plataforma",
            "Acesso total, inclusive gestão de todas as empresas.", AllPermissions),
        new(2, Administrator, "Administrador",
            "Acesso total à própria empresa.",
            AllPermissions.Where(p => p != Permissions.Companies.Manage).ToArray()),
        new(3, FleetManager, "Gestor de frota",
            "Gerencia a frota e a operação: cadastros, alocações, hodômetro, documentos, checklists, ocorrências, manutenção, combustível e pneus.",
            [Permissions.Dashboard.View, Permissions.Companies.View, Permissions.Users.View, Permissions.Roles.View,
             Permissions.Audit.View, .. FleetRegistryFull, .. OperationsFull, .. MaintenanceFull, .. FuelFull, .. TiresFull]),
        new(4, Operations, "Operações",
            "Operação diária: motoristas, alocações, hodômetro, documentos, checklists, ocorrências, abastecimentos e inspeção de pneus.",
            [Permissions.Dashboard.View, Permissions.Drivers.View, Permissions.Drivers.Create, Permissions.Drivers.Update,
             Permissions.Vehicles.View, Permissions.Vehicles.Update, Permissions.Implements.View, Permissions.Implements.Update,
             .. OperationsDaily, Permissions.Maintenance.View, Permissions.Maintenance.CreateRequest,
             // Registers fuelings (and sees what they typed), but not the fleet's fuel spending.
             Permissions.Fuel.View, Permissions.Fuel.Create,
             // Field inspection of tires (tread, pressure, damage photos); moving tires is the tire shop's job.
             Permissions.Tires.View, Permissions.Tires.Inspect]),
        new(5, Maintenance, "Manutenção",
            "Atualiza a situação de veículos e implementos, acompanha ocorrências e executa a manutenção da frota, inclusive a borracharia.",
            [Permissions.Dashboard.View, Permissions.Vehicles.View, Permissions.Vehicles.Update,
             Permissions.Implements.View, Permissions.Implements.Update, Permissions.Occurrences.View, .. MaintenanceFull,
             // Consumption is a maintenance signal (seção 36); money is not their concern.
             Permissions.Fuel.View,
             // The tire shop is part of maintenance: the whole lifecycle, costs included (same as maintenance.viewcosts).
             .. TiresFull]),
        new(6, Finance, "Financeiro",
            "Consulta a frota e os custos de manutenção, combustível e pneus.",
            [Permissions.Dashboard.View, .. FleetRegistryRead, Permissions.Maintenance.View, Permissions.Maintenance.ViewCosts,
             Permissions.Fuel.View, Permissions.Fuel.ViewCosts, Permissions.Tires.View, Permissions.Tires.ViewCosts]),
        new(7, Driver, "Motorista",
            "Reservado para o app do motorista (fases futuras).", []),
        new(8, Viewer, "Visualizador",
            "Somente leitura.",
            [Permissions.Dashboard.View, Permissions.Companies.View, .. FleetRegistryRead, .. OperationsRead, Permissions.Maintenance.View,
             Permissions.Fuel.View, Permissions.Tires.View]),
    ];
}
