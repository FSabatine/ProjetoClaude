using Fleet.Application.Files;
using Fleet.Application.Maintenance;
using Fleet.Application.Mileage;
using Fleet.Application.Operations;
using Fleet.Application.Tires;
using Fleet.Infrastructure.Persistence;

namespace Fleet.Application.Tests.TestSupport;

/// <summary>
/// Phase 5 services built exactly as DI would. Every builder takes an optional context so concurrency tests can run two
/// "requests" (two DbContexts) against the same database.
/// </summary>
public static class TireServices
{
    private static OperationalEventLog Events(TestDb t, FleetDbContext db) => new(db, t.Clock, t.CurrentUser);

    private static MileageService Mileage(TestDb t, FleetDbContext db) => new(
        db, t.Clock, t.CurrentUser, Events(t, db), new OdometerReadingRequestValidator(t.Clock), new OdometerReviewRequestValidator());

    private static FileService Files(TestDb t, FleetDbContext db) => new(db, t.Storage, t.CurrentUser, t.Clock);

    private static MaintenanceRequestService MaintenanceRequests(TestDb t, FleetDbContext db)
    {
        var workOrders = new WorkOrderService(db, t.Clock, t.CurrentUser, Events(t, db), new MaintenanceScheduleService(db, t.Clock),
            new WorkOrderRequestValidator(), new WorkOrderStatusRequestValidator(), new WorkOrderPartRequestValidator(), new WorkOrderLaborRequestValidator());
        return new MaintenanceRequestService(db, t.Clock, t.CurrentUser, Events(t, db), workOrders, new MaintenanceRequestRequestValidator(t.Clock),
            new MaintenanceRequestRejectRequestValidator());
    }

    public static TireModelService Models(TestDb t, FleetDbContext? db = null) => new(db ?? t.Db, new TireModelRequestValidator());

    public static TireLayoutService Layouts(TestDb t, FleetDbContext? db = null) => new(db ?? t.Db, t.CurrentUser, new TireLayoutRequestValidator());

    public static TireSettingsService Settings(TestDb t, FleetDbContext? db = null) => new(db ?? t.Db, new TireSettingsRequestValidator());

    public static TireLifecycle Lifecycle(TestDb t, FleetDbContext? db = null)
    {
        var d = db ?? t.Db;
        return new TireLifecycle(d, t.Clock, t.CurrentUser, Mileage(t, d), Events(t, d), Layouts(t, d));
    }

    public static TireMonitoring Monitoring(TestDb t, FleetDbContext? db = null) => new(db ?? t.Db, Lifecycle(t, db));

    public static TireService Tires(TestDb t, FleetDbContext? db = null)
    {
        var d = db ?? t.Db;
        return new TireService(d, t.Clock, t.CurrentUser, Files(t, d), Lifecycle(t, d), Monitoring(t, d), Settings(t, d),
            new OperationalHistoryService(d, t.Clock, t.CurrentUser, new HistoryRequestValidator()), new TireRequestValidator(t.Clock), new TireListRequestValidator());
    }

    public static TireOperationsService Operations(TestDb t, FleetDbContext? db = null)
    {
        var d = db ?? t.Db;
        return new TireOperationsService(d, Lifecycle(t, d), Monitoring(t, d), Tires(t, d), Settings(t, d), Files(t, d),
            new TireInstallRequestValidator(), new TireRemovalRequestValidator(), new TireReplaceRequestValidator(), new TireTransferRequestValidator(),
            new TireRotationRequestValidator(), new TireStockRequestValidator(), new TireDisposalRequestValidator(),
            new TireInstallationCorrectionRequestValidator());
    }

    public static TireInspectionService Inspections(TestDb t, FleetDbContext? db = null)
    {
        var d = db ?? t.Db;
        return new TireInspectionService(d, Lifecycle(t, d), Monitoring(t, d), Tires(t, d), Settings(t, d), MaintenanceRequests(t, d), Files(t, d),
            new TireInspectionRequestValidator());
    }

    public static TireServiceOrderService ServiceOrders(TestDb t, FleetDbContext? db = null)
    {
        var d = db ?? t.Db;
        return new TireServiceOrderService(d, Lifecycle(t, d), Monitoring(t, d), Tires(t, d), Settings(t, d), Files(t, d),
            new TireServiceRequestValidator(), new TireServiceCompletionRequestValidator(), new TireCostRequestValidator(t.Clock));
    }

    public static TireAnalyticsService Analytics(TestDb t, FleetDbContext? db = null)
    {
        var d = db ?? t.Db;
        return new TireAnalyticsService(d, t.Clock, Lifecycle(t, d), Tires(t, d), Settings(t, d), new TireReportRequestValidator());
    }
}
