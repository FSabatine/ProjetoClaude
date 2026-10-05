using Fleet.Domain.Common;

namespace Fleet.Domain.Finance;

/// <summary>
/// Where a cost belongs (department, branch, project…). Hierarchical and configurable per company (ADR-040) —
/// the spec's "Branch São Paulo" example is just a leaf cost center, so there is no separate Branch entity.
/// </summary>
public class CostCenter : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentCostCenterId { get; set; }
    public CostCenter? ParentCostCenter { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
