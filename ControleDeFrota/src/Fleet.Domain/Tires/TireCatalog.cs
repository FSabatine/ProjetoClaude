using Fleet.Domain.Common;

namespace Fleet.Domain.Tires;

/// <summary>What the tire was designed for — informs compatibility warnings, never blocks by itself.</summary>
public enum TireApplication
{
    /// <summary>Direcional (eixo dianteiro).</summary>
    Steer,
    /// <summary>Tração.</summary>
    Drive,
    /// <summary>Reboque / eixo livre.</summary>
    Trailer,
    /// <summary>Uso misto (qualquer posição).</summary>
    AllPosition,
}

public enum TireConstruction
{
    Radial,
    Bias,
}

/// <summary>
/// Catalog entry: brand + model + size and the specs every tire of the model shares (ADR-035). Brand and specification
/// are attributes of the model, not entities of their own: nothing else references them and the brand list for
/// filters comes from DISTINCT Brand.
/// </summary>
public class TireModel : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int BrandMaxLength = 60;
    public const int NameMaxLength = 80;
    public const int SizeMaxLength = 30;
    public const int RatingMaxLength = 10;
    public const int NotesMaxLength = 500;

    public Guid CompanyId { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Normalized size ("295/80R22.5") — compared with the axle's allowed size.</summary>
    public string Size { get; set; } = string.Empty;
    public TireApplication Application { get; set; } = TireApplication.AllPosition;
    public TireConstruction Construction { get; set; } = TireConstruction.Radial;
    /// <summary>Load index as printed on the sidewall ("152/148").</summary>
    public string? LoadIndex { get; set; }
    /// <summary>Speed symbol ("L", "M").</summary>
    public string? SpeedRating { get; set; }
    /// <summary>Tread depth of a new tire of this model (mm) — default for the tires registered with it.</summary>
    public decimal? OriginalTreadDepthMm { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Which kind of asset a layout describes.</summary>
public enum TireLayoutTarget
{
    Vehicle,
    Implement,
}

public enum AxleType
{
    /// <summary>Direcional.</summary>
    Steer,
    /// <summary>Tração.</summary>
    Drive,
    /// <summary>Livre (truck, sem tração).</summary>
    Free,
    /// <summary>Eixo de implemento.</summary>
    Trailer,
}

public enum TireSide
{
    Left,
    Right,
    /// <summary>Spare wheel.</summary>
    None,
}

public enum TirePlacement
{
    Single,
    Outer,
    Inner,
    Spare,
}

/// <summary>
/// Configurable axle layout ("configuração de eixos", seções 8–10) shared by the vehicles or implements that point to it.
/// Positions are generated from the axles (<see cref="TirePositions.For"/>) with stable codes — installations store the
/// code and a label snapshot, so history stays readable whatever happens to the layout later.
/// </summary>
public class TireLayout : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 300;
    public const int MaxAxles = 10;
    public const int MaxSpares = 2;

    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TireLayoutTarget Target { get; set; }
    public string? Description { get; set; }
    public int SpareCount { get; set; }
    public bool IsActive { get; set; } = true;
    public List<TireLayoutAxle> Axles { get; set; } = [];

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>One axle of a layout: single (2 tires) or dual (4 tires), with what its positions require.</summary>
public class TireLayoutAxle : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid TireLayoutId { get; set; }
    /// <summary>1 = front-most axle.</summary>
    public int Number { get; set; }
    public AxleType Type { get; set; }
    public bool IsDual { get; set; }
    /// <summary>False for a lift axle or an optional position — an empty optional position is not flagged.</summary>
    public bool IsRequired { get; set; } = true;
    /// <summary>Normalized size; null = any size (compatibility then "could not be verified").</summary>
    public string? AllowedSize { get; set; }
    /// <summary>Reference pressure for this axle, in psi. Null = pressure readings are recorded but not compared.</summary>
    public decimal? RecommendedPressurePsi { get; set; }
}

/// <summary>A wheel position generated from a layout.</summary>
public sealed record TirePosition(
    string Code, string Label, int AxleNumber, AxleType? AxleType, TireSide Side, TirePlacement Placement, bool IsRequired,
    string? AllowedSize, decimal? RecommendedPressurePsi)
{
    public bool IsSpare => Placement == TirePlacement.Spare;
}

/// <summary>
/// Position codes (pt-BR, shown on the diagram): axle number + side (E/D) + outer/inner (E/I) on dual axles; "EST1" for spares.
/// Single axle 1 → 1E, 1D. Dual axle 2 → 2EE, 2EI, 2DI, 2DE (left to right, as seen from above).
/// </summary>
public static class TirePositions
{
    public static IReadOnlyList<TirePosition> For(TireLayout layout)
    {
        var positions = new List<TirePosition>();
        foreach (var axle in layout.Axles.OrderBy(a => a.Number))
        {
            var n = axle.Number;
            if (axle.IsDual)
            {
                positions.Add(Make(axle, $"{n}EE", "Esquerdo externo", TireSide.Left, TirePlacement.Outer));
                positions.Add(Make(axle, $"{n}EI", "Esquerdo interno", TireSide.Left, TirePlacement.Inner));
                positions.Add(Make(axle, $"{n}DI", "Direito interno", TireSide.Right, TirePlacement.Inner));
                positions.Add(Make(axle, $"{n}DE", "Direito externo", TireSide.Right, TirePlacement.Outer));
            }
            else
            {
                positions.Add(Make(axle, $"{n}E", "Esquerdo", TireSide.Left, TirePlacement.Single));
                positions.Add(Make(axle, $"{n}D", "Direito", TireSide.Right, TirePlacement.Single));
            }
        }
        for (var s = 1; s <= layout.SpareCount; s++)
            positions.Add(new TirePosition($"EST{s}", layout.SpareCount == 1 ? "Estepe" : $"Estepe {s}", 0, null, TireSide.None,
                TirePlacement.Spare, IsRequired: false, AllowedSize: null, RecommendedPressurePsi: null));
        return positions;
    }

    private static TirePosition Make(TireLayoutAxle axle, string code, string side, TireSide tireSide, TirePlacement placement) =>
        new(code, $"Eixo {axle.Number} — {side}", axle.Number, axle.Type, tireSide, placement, axle.IsRequired, axle.AllowedSize,
            axle.RecommendedPressurePsi);

    /// <summary>The other tire of the same side on a dual axle (2EE ↔ 2EI) — used to warn about mismatched pairs.</summary>
    public static string? DualPartner(TirePosition position) => position.Placement switch
    {
        TirePlacement.Outer => position.Code[..^1] + "I",
        TirePlacement.Inner => position.Code[..^1] + "E",
        _ => null,
    };
}

/// <summary>Layouts every company starts with (seeded on first read, like the document types and fuel types).</summary>
public static class TireLayoutDefaults
{
    public sealed record AxleDefault(AxleType Type, bool IsDual, bool IsRequired = true);

    public sealed record LayoutDefault(string Name, TireLayoutTarget Target, int SpareCount, string Description, AxleDefault[] Axles);

    public static readonly IReadOnlyList<LayoutDefault> All =
    [
        new("Carro / picape (2 eixos simples)", TireLayoutTarget.Vehicle, 1, "Veículo leve: 4 pneus + estepe.",
            [new(AxleType.Steer, false), new(AxleType.Drive, false)]),
        new("Caminhão toco 4x2", TireLayoutTarget.Vehicle, 1, "Eixo direcional simples + eixo de tração duplo: 6 pneus + estepe.",
            [new(AxleType.Steer, false), new(AxleType.Drive, true)]),
        new("Caminhão truck 6x2", TireLayoutTarget.Vehicle, 1, "Direcional simples, tração dupla e eixo livre duplo: 10 pneus + estepe.",
            [new(AxleType.Steer, false), new(AxleType.Drive, true), new(AxleType.Free, true)]),
        new("Cavalo mecânico 6x2", TireLayoutTarget.Vehicle, 1, "Direcional simples, tração dupla e eixo livre duplo (suspensível).",
            [new(AxleType.Steer, false), new(AxleType.Drive, true), new(AxleType.Free, true, IsRequired: false)]),
        new("Cavalo mecânico 6x4", TireLayoutTarget.Vehicle, 1, "Direcional simples e dois eixos de tração duplos.",
            [new(AxleType.Steer, false), new(AxleType.Drive, true), new(AxleType.Drive, true)]),
        new("Semirreboque 3 eixos", TireLayoutTarget.Implement, 1, "Três eixos duplos: 12 pneus + estepe.",
            [new(AxleType.Trailer, true), new(AxleType.Trailer, true), new(AxleType.Trailer, true)]),
        new("Semirreboque 2 eixos", TireLayoutTarget.Implement, 1, "Dois eixos duplos: 8 pneus + estepe.",
            [new(AxleType.Trailer, true), new(AxleType.Trailer, true)]),
    ];
}
