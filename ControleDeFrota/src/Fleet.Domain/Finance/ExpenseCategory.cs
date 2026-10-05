using Fleet.Domain.Common;

namespace Fleet.Domain.Finance;

/// <summary>
/// Configurable per company (ADR-040), hierarchical. The three categories that have an operational source
/// (Fuel, Maintenance, Tires) are <see cref="IsSystemCategory"/>: they are seeded once, cannot be renamed in a
/// way that breaks the link, and cannot be deleted — <see cref="CostAggregationKey"/> is how the aggregation
/// service recognizes them without a hardcoded id.
/// </summary>
public class ExpenseCategory : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int CodeMaxLength = 30;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public ExpenseCategory? ParentCategory { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Seeded, not user-created. Fuel/Maintenance/Tires: fed by their own module, never by a manual expense.</summary>
    public bool IsSystemCategory { get; set; }
    /// <summary>Null for a regular (manual) category. Set only on the three system categories.</summary>
    public CostAggregationSource? CostAggregationKey { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>The operational tables a blended cost report reads besides the manual Expense ledger (ADR-040).</summary>
public enum CostAggregationSource
{
    Fuel,
    Maintenance,
    Tires,
}

/// <summary>
/// Default catalog created once per company (same pattern as FuelTypeDefaults/DocumentTypeDefaults): the three
/// system categories plus the common operational expense categories from the spec, organized under two parents.
/// </summary>
public static class ExpenseCategoryDefaults
{
    public sealed record Default(string Name, string Code, string? ParentCode, bool IsSystem = false, CostAggregationSource? Source = null);

    public static readonly IReadOnlyList<Default> All =
    [
        new("Custos de veículo", "VEHICLE-COSTS", null),
        new("Combustível", "FUEL", "VEHICLE-COSTS", true, CostAggregationSource.Fuel),
        new("Manutenção", "MAINTENANCE", "VEHICLE-COSTS", true, CostAggregationSource.Maintenance),
        new("Pneus", "TIRES", "VEHICLE-COSTS", true, CostAggregationSource.Tires),
        new("Seguro", "INSURANCE", "VEHICLE-COSTS"),
        new("IPVA", "IPVA", "VEHICLE-COSTS"),
        new("Licenciamento", "LICENSING", "VEHICLE-COSTS"),
        new("Multas", "FINES", "VEHICLE-COSTS"),
        new("Financiamento", "FINANCING", "VEHICLE-COSTS"),
        new("Leasing", "LEASING", "VEHICLE-COSTS"),
        new("Locação de veículo", "RENTAL", "VEHICLE-COSTS"),
        new("Socorro / guincho", "ROADSIDE-ASSISTANCE", "VEHICLE-COSTS"),
        new("Custos operacionais", "OPERATIONAL-COSTS", null),
        new("Pedágio", "TOLLS", "OPERATIONAL-COSTS"),
        new("Estacionamento", "PARKING", "OPERATIONAL-COSTS"),
        new("Lavagem", "WASHING", "OPERATIONAL-COSTS"),
        new("Outros", "OTHER", "OPERATIONAL-COSTS"),
    ];
}
