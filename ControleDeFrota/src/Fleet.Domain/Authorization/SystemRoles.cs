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

    public static readonly IReadOnlyList<RoleDefinition> All =
    [
        new(1, PlatformAdministrator, "Administrador da plataforma",
            "Acesso total, inclusive gestão de todas as empresas.", AllPermissions),
        new(2, Administrator, "Administrador",
            "Acesso total à própria empresa.",
            AllPermissions.Where(p => p != Permissions.Companies.Manage).ToArray()),
        new(3, FleetManager, "Gestor de frota",
            "Gerencia a frota e a operação: cadastros, alocações, hodômetro, documentos, checklists e ocorrências.",
            [Permissions.Dashboard.View, Permissions.Companies.View, Permissions.Users.View, Permissions.Roles.View,
             Permissions.Audit.View, .. FleetRegistryFull, .. OperationsFull]),
        new(4, Operations, "Operações",
            "Operação diária: motoristas, alocações, hodômetro, documentos, checklists e ocorrências.",
            [Permissions.Dashboard.View, Permissions.Drivers.View, Permissions.Drivers.Create, Permissions.Drivers.Update,
             Permissions.Vehicles.View, Permissions.Vehicles.Update, Permissions.Implements.View, Permissions.Implements.Update,
             .. OperationsDaily]),
        new(5, Maintenance, "Manutenção",
            "Atualiza a situação de veículos e implementos e acompanha as ocorrências operacionais.",
            [Permissions.Dashboard.View, Permissions.Vehicles.View, Permissions.Vehicles.Update,
             Permissions.Implements.View, Permissions.Implements.Update, Permissions.Occurrences.View]),
        new(6, Finance, "Financeiro",
            "Consulta a frota para fins financeiros.",
            [Permissions.Dashboard.View, .. FleetRegistryRead]),
        new(7, Driver, "Motorista",
            "Reservado para o app do motorista (fases futuras).", []),
        new(8, Viewer, "Visualizador",
            "Somente leitura.",
            [Permissions.Dashboard.View, Permissions.Companies.View, .. FleetRegistryRead, .. OperationsRead]),
    ];
}
