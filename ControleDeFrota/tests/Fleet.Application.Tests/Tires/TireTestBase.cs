using Fleet.Application.Mileage;
using Fleet.Application.Tests.TestSupport;
using Fleet.Application.Tires;
using Fleet.Domain.Authorization;
using Fleet.Domain.Companies;
using Fleet.Domain.Tires;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Tires;

/// <summary>
/// Shared arrangement: a truck registered with 100,000 km using the default "Caminhão toco 4x2" layout
/// (1E, 1D | 2EE, 2EI, 2DI, 2DE | EST1) and a 295/80R22.5 tire model with 16 mm of original tread.
/// </summary>
public abstract class TireTestBase : IDisposable
{
    protected readonly TestDb T = new();
    protected Company Company = null!;
    protected Guid VehicleId;
    protected Guid ModelId;
    protected Guid LayoutId;

    public void Dispose() => T.Dispose();

    protected TireService Tires => TireServices.Tires(T);
    protected TireOperationsService Operations => TireServices.Operations(T);
    protected TireInspectionService Inspections => TireServices.Inspections(T);
    protected TireServiceOrderService ServiceOrders => TireServices.ServiceOrders(T);
    protected TireAnalyticsService Analytics => TireServices.Analytics(T);

    protected async Task ArrangeAsync(string role = SystemRoles.FleetManager, string? allowedSteerSize = null)
    {
        Company = await Scenario.SignedInAsync(T);
        VehicleId = (await Scenario.VehicleAsync(T, change: r => r with { CurrentOdometerKm = 100_000 })).Id;
        ModelId = (await TireServices.Models(T).CreateAsync(new TireModelRequest
        {
            Brand = "Michelin", Name = "X Multi Z", Size = "295/80 R22.5", Application = TireApplication.AllPosition, OriginalTreadDepthMm = 16,
        }, default)).Id;
        var layouts = await TireServices.Layouts(T).ListAsync(TireLayoutTarget.Vehicle, false, default);
        LayoutId = layouts.Single(l => l.Name == "Caminhão toco 4x2").Id;
        if (allowedSteerSize is not null)
        {
            var layout = layouts.Single(l => l.Id == LayoutId);
            await TireServices.Layouts(T).UpdateAsync(LayoutId, new TireLayoutRequest
            {
                Name = layout.Name, Target = layout.Target, SpareCount = layout.SpareCount,
                Axles = layout.Axles.Select(a => new TireLayoutAxleRequest
                {
                    Type = a.Type, IsDual = a.IsDual, IsRequired = a.IsRequired, RecommendedPressurePsi = 110,
                    AllowedSize = a.Number == 1 ? allowedSteerSize : null,
                }).ToList(),
            }, default);
        }
        await Operations.SetLayoutAsync(VehicleId, null, new TireLayoutAssignmentRequest { LayoutId = LayoutId }, default);
        if (role != SystemRoles.FleetManager) T.SignInAs(Company, role);
    }

    protected Task<TireResponse> NewTireAsync(string? code = null, decimal? price = 2_000m, Guid? modelId = null, int retreads = 0) =>
        Tires.CreateAsync(new TireRequest
        {
            Code = code, TireModelId = modelId ?? ModelId, PurchasePrice = price, PurchasedOn = T.Clock.Today, Dot = "DOT 4B7X 3524",
            RetreadCount = retreads, StorageLocation = "Almoxarifado A",
        }, default);

    protected Task<TireResponse> InstallAsync(Guid tireId, string position, Guid? vehicleId = null, int? km = null, DateTime? at = null) =>
        Operations.InstallAsync(tireId, new TireInstallRequest
        {
            VehicleId = vehicleId ?? VehicleId, PositionCode = position, OdometerKm = km, PerformedAt = at,
        }, default);

    protected Task<TireResponse> RemoveAsync(Guid tireId, TireRemovalDestination destination = TireRemovalDestination.Stock,
        TireRemovalReason reason = TireRemovalReason.Inspection, decimal? tread = null, TireDisposalReason? disposal = null) =>
        Operations.RemoveAsync(tireId, new TireRemovalRequest
        {
            Reason = reason, Destination = destination, TreadDepthMm = tread, DisposalReason = disposal, StorageLocation = "Almoxarifado B",
        }, default);

    /// <summary>Moves the clock and records a valid odometer reading (500 km/day keeps it below the suspicious-jump rule).</summary>
    protected async Task DriveToAsync(int km, Guid? vehicleId = null)
    {
        var current = await T.Db.Vehicles.Where(v => v.Id == (vehicleId ?? VehicleId)).Select(v => v.CurrentOdometerKm).SingleAsync();
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(Math.Max(1, Math.Ceiling((km - current) / 500.0)));
        await Services.Mileage(T).RecordAsync(vehicleId ?? VehicleId, new OdometerReadingRequest { OdometerKm = km }, default);
    }

    protected async Task<Tire> ReloadAsync(Guid tireId)
    {
        await using var db = T.NewContext();
        return await db.Tires.Include(t => t.Model).SingleAsync(t => t.Id == tireId);
    }
}
