namespace Fleet.Domain.Authorization;

/// <summary>
/// Permission catalog — the single source of permission keys ("module.action").
/// New permission: add the constant here, a PermissionCatalog entry, map it in SystemRoles,
/// create a migration and protect the endpoint with [HasPermission].
/// </summary>
public static class Permissions
{
    public static class Dashboard
    {
        public const string View = "dashboard.view";
    }

    public static class Companies
    {
        /// <summary>View the user's own company.</summary>
        public const string View = "companies.view";
        /// <summary>Edit the user's own company.</summary>
        public const string Update = "companies.update";
        /// <summary>Platform super-admin: list, create, edit and delete any company (ADR-003).</summary>
        public const string Manage = "companies.manage";
    }

    public static class Users
    {
        public const string View = "users.view";
        public const string Manage = "users.manage";
    }

    public static class Roles
    {
        public const string View = "roles.view";
    }

    public static class Drivers
    {
        public const string View = "drivers.view";
        public const string Create = "drivers.create";
        public const string Update = "drivers.update";
        public const string Delete = "drivers.delete";
    }

    public static class Vehicles
    {
        public const string View = "vehicles.view";
        public const string Create = "vehicles.create";
        public const string Update = "vehicles.update";
        public const string Delete = "vehicles.delete";
    }

    public static class Implements
    {
        public const string View = "implements.view";
        public const string Create = "implements.create";
        public const string Update = "implements.update";
        public const string Delete = "implements.delete";
    }

    public static class Audit
    {
        public const string View = "audit.view";
    }
}

public sealed record PermissionDefinition(int Id, string Key, string Description)
{
    public string Module => Key[..Key.IndexOf('.')];
}

/// <summary>Seeded into the Permissions table. Ids are stable — never renumber.</summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(1, Permissions.Dashboard.View, "Visualizar o painel"),
        new(10, Permissions.Companies.View, "Visualizar a própria empresa"),
        new(11, Permissions.Companies.Update, "Editar a própria empresa"),
        new(12, Permissions.Companies.Manage, "Gerenciar todas as empresas (plataforma)"),
        new(20, Permissions.Users.View, "Visualizar usuários"),
        new(21, Permissions.Users.Manage, "Criar, editar e excluir usuários"),
        new(30, Permissions.Roles.View, "Visualizar papéis e permissões"),
        new(40, Permissions.Drivers.View, "Visualizar motoristas"),
        new(41, Permissions.Drivers.Create, "Cadastrar motoristas"),
        new(42, Permissions.Drivers.Update, "Editar motoristas"),
        new(43, Permissions.Drivers.Delete, "Excluir motoristas"),
        new(50, Permissions.Vehicles.View, "Visualizar veículos"),
        new(51, Permissions.Vehicles.Create, "Cadastrar veículos"),
        new(52, Permissions.Vehicles.Update, "Editar veículos"),
        new(53, Permissions.Vehicles.Delete, "Excluir veículos"),
        new(60, Permissions.Implements.View, "Visualizar implementos"),
        new(61, Permissions.Implements.Create, "Cadastrar implementos"),
        new(62, Permissions.Implements.Update, "Editar implementos"),
        new(63, Permissions.Implements.Delete, "Excluir implementos"),
        new(90, Permissions.Audit.View, "Visualizar histórico de alterações"),
    ];

    public static PermissionDefinition Get(string key) => All.Single(p => p.Key == key);
}
