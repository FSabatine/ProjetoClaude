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

    public static class Assignments
    {
        public const string View = "assignments.view";
        /// <summary>Assign, change and end the driver of a vehicle.</summary>
        public const string Manage = "assignments.manage";
    }

    public static class Mileage
    {
        /// <summary>Record a new odometer reading.</summary>
        public const string Record = "mileage.record";
        /// <summary>Review suspicious readings and register audited corrections.</summary>
        public const string Manage = "mileage.manage";
    }

    public static class Documents
    {
        public const string View = "documents.view";
        /// <summary>Create, edit and renew documents and their files.</summary>
        public const string Manage = "documents.manage";
        public const string Delete = "documents.delete";
    }

    public static class Checklists
    {
        /// <summary>See executed checklists.</summary>
        public const string View = "checklists.view";
        public const string Execute = "checklists.execute";
    }

    public static class Occurrences
    {
        public const string View = "occurrences.view";
        public const string Create = "occurrences.create";
        /// <summary>Edit, analyse, resolve and cancel occurrences.</summary>
        public const string Manage = "occurrences.manage";
    }

    public static class Operations
    {
        /// <summary>Configure document types and checklist templates of the company.</summary>
        public const string Configure = "operations.configure";
    }

    public static class Audit
    {
        public const string View = "audit.view";
    }

    public static class Maintenance
    {
        public const string View = "maintenance.view";
        /// <summary>Report a problem / ask for maintenance (driver report, occurrence, manager).</summary>
        public const string CreateRequest = "maintenance.createrequest";
        /// <summary>Configure preventive maintenance plans and their items.</summary>
        public const string ManagePlans = "maintenance.manageplans";
        /// <summary>Approve/reject requests, create, schedule, assign, execute and close work orders.</summary>
        public const string ManageWorkOrders = "maintenance.manageworkorders";
        public const string ManageWorkshops = "maintenance.manageworkshops";
        /// <summary>See parts/labor/total cost figures.</summary>
        public const string ViewCosts = "maintenance.viewcosts";
    }

    public static class Fuel
    {
        /// <summary>Fuelings, consumption, stations, fuel types and reports — without money values (see ViewCosts).</summary>
        public const string View = "fuel.view";
        public const string Create = "fuel.create";
        /// <summary>Audited correction of a fueling (reason required). Changing its odometer also needs mileage.manage.</summary>
        public const string Correct = "fuel.correct";
        public const string Cancel = "fuel.cancel";
        public const string ReviewAnomalies = "fuel.reviewanomalies";
        /// <summary>Fuel stations and their reference prices.</summary>
        public const string ManageStations = "fuel.managestations";
        /// <summary>Fuel type catalog and anomaly thresholds.</summary>
        public const string Configure = "fuel.configure";
        /// <summary>Prices, totals and every cost figure (dashboard, reports, cost/km). Own records are always visible to their author.</summary>
        public const string ViewCosts = "fuel.viewcosts";
    }

    public static class Tires
    {
        public const string View = "tires.view";
        /// <summary>Register tires (and new catalog models while registering).</summary>
        public const string Create = "tires.create";
        /// <summary>Edit registry data and correct lifecycle history (reason required).</summary>
        public const string Edit = "tires.edit";
        /// <summary>Install, replace (installation side) and move tires between assets.</summary>
        public const string Install = "tires.install";
        public const string Remove = "tires.remove";
        public const string Rotate = "tires.rotate";
        public const string Inspect = "tires.inspect";
        public const string Repair = "tires.repair";
        public const string Retread = "tires.retread";
        public const string Dispose = "tires.dispose";
        /// <summary>Purchase price, service costs, lifecycle cost and cost/km (also required to type a cost).</summary>
        public const string ViewCosts = "tires.viewcosts";
        /// <summary>Axle configurations, tire model catalog and tire policy thresholds.</summary>
        public const string ManageSettings = "tires.managesettings";
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
        new(100, Permissions.Assignments.View, "Visualizar alocações de motoristas"),
        new(101, Permissions.Assignments.Manage, "Alocar, trocar e encerrar motoristas de veículos"),
        new(110, Permissions.Mileage.Record, "Registrar leituras de hodômetro"),
        new(111, Permissions.Mileage.Manage, "Revisar leituras suspeitas e corrigir hodômetro"),
        new(120, Permissions.Documents.View, "Visualizar documentos"),
        new(121, Permissions.Documents.Manage, "Cadastrar, editar e renovar documentos"),
        new(122, Permissions.Documents.Delete, "Excluir documentos"),
        new(130, Permissions.Checklists.View, "Visualizar checklists realizados"),
        new(131, Permissions.Checklists.Execute, "Realizar checklists"),
        new(140, Permissions.Occurrences.View, "Visualizar ocorrências"),
        new(141, Permissions.Occurrences.Create, "Registrar ocorrências"),
        new(142, Permissions.Occurrences.Manage, "Analisar, resolver e cancelar ocorrências"),
        new(150, Permissions.Operations.Configure, "Configurar tipos de documento e modelos de checklist"),
        new(160, Permissions.Maintenance.View, "Visualizar manutenção (planos, solicitações, ordens de serviço)"),
        new(161, Permissions.Maintenance.CreateRequest, "Solicitar manutenção"),
        new(162, Permissions.Maintenance.ManagePlans, "Configurar planos de manutenção preventiva"),
        new(163, Permissions.Maintenance.ManageWorkOrders, "Aprovar solicitações e gerenciar ordens de serviço"),
        new(164, Permissions.Maintenance.ManageWorkshops, "Cadastrar e editar oficinas"),
        new(165, Permissions.Maintenance.ViewCosts, "Visualizar custos de manutenção"),
        new(170, Permissions.Fuel.View, "Visualizar abastecimentos, consumo e relatórios de combustível"),
        new(171, Permissions.Fuel.Create, "Registrar abastecimentos"),
        new(172, Permissions.Fuel.Correct, "Corrigir abastecimentos"),
        new(173, Permissions.Fuel.Cancel, "Cancelar abastecimentos"),
        new(174, Permissions.Fuel.ReviewAnomalies, "Revisar abastecimentos com alerta"),
        new(175, Permissions.Fuel.ManageStations, "Cadastrar postos e preços de referência"),
        new(176, Permissions.Fuel.Configure, "Configurar tipos de combustível e limites de alerta"),
        new(177, Permissions.Fuel.ViewCosts, "Visualizar custos de combustível"),
        new(180, Permissions.Tires.View, "Visualizar pneus, histórico, painel e relatórios de pneus"),
        new(181, Permissions.Tires.Create, "Cadastrar pneus"),
        new(182, Permissions.Tires.Edit, "Editar pneus e corrigir o histórico de pneus"),
        new(183, Permissions.Tires.Install, "Instalar, substituir e transferir pneus"),
        new(184, Permissions.Tires.Remove, "Remover pneus"),
        new(185, Permissions.Tires.Rotate, "Fazer rodízio de pneus"),
        new(186, Permissions.Tires.Inspect, "Inspecionar pneus (sulco, pressão, danos)"),
        new(187, Permissions.Tires.Repair, "Registrar consertos de pneus"),
        new(188, Permissions.Tires.Retread, "Registrar recapagens de pneus"),
        new(189, Permissions.Tires.Dispose, "Dar baixa em pneus"),
        new(190, Permissions.Tires.ViewCosts, "Visualizar custos de pneus"),
        new(191, Permissions.Tires.ManageSettings, "Configurar eixos, modelos e limites de pneus"),
    ];

    public static PermissionDefinition Get(string key) => All.Single(p => p.Key == key);
}
