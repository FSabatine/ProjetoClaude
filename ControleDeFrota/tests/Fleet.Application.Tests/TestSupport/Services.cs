using Fleet.Application.Assignments;
using Fleet.Application.Auth;
using Fleet.Application.Checklists;
using Fleet.Application.Documents;
using Fleet.Application.Files;
using Fleet.Application.Fuel;
using Fleet.Application.Mileage;
using Fleet.Application.Occurrences;
using Fleet.Application.Operations;
using Fleet.Application.Companies;
using Fleet.Application.Dashboard;
using Fleet.Application.Drivers;
using Fleet.Application.Implements;
using Fleet.Application.Maintenance;
using Fleet.Application.Users;
using Fleet.Application.Vehicles;
using Fleet.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fleet.Application.Tests.TestSupport;

/// <summary>Builds services exactly as DI would, over a TestDb.</summary>
public static class Services
{
    public static readonly PasswordHasher Hasher = new();

    public static VehicleService Vehicles(TestDb t) => new(t.Db, t.Clock, Events(t), new VehicleRequestValidator(t.Clock));
    public static ImplementService Implements(TestDb t) => new(t.Db, new ImplementRequestValidator(t.Clock));
    public static DriverService Drivers(TestDb t) => new(t.Db, t.Clock, new DriverRequestValidator(t.Clock));
    public static CompanyService Companies(TestDb t) => new(t.Db, t.CurrentUser, new CompanyRequestValidator());
    public static DashboardService Dashboard(TestDb t) => new(t.Db, t.Clock, t.CurrentUser, Checklists(t));

    // Phase 2
    public static OperationalEventLog Events(TestDb t) => new(t.Db, t.Clock, t.CurrentUser);
    public static OperationalHistoryService History(TestDb t) => new(t.Db, t.Clock, new HistoryRequestValidator());
    public static AssignmentService Assignments(TestDb t) => new(
        t.Db, t.Clock, Events(t), new AssignmentCreateRequestValidator(t.Clock), new AssignmentEndRequestValidator(t.Clock));
    public static MileageService Mileage(TestDb t) => new(
        t.Db, t.Clock, t.CurrentUser, Events(t), new OdometerReadingRequestValidator(t.Clock), new OdometerReviewRequestValidator());
    public static FileService Files(TestDb t) => new(t.Db, t.Storage, t.CurrentUser, t.Clock);
    public static DocumentTypeService DocumentTypes(TestDb t) => new(t.Db, t.CurrentUser, new DocumentTypeRequestValidator());
    public static DocumentService Documents(TestDb t) => new(
        t.Db, t.Clock, t.CurrentUser, Files(t), Events(t), new DocumentListRequestValidator(),
        new DocumentCreateRequestValidator(t.Clock), new DocumentUpdateRequestValidator(t.Clock));
    public static DocumentExpirationScanner DocumentScanner(TestDb t) => new(t.Db, t.Clock);
    public static ChecklistTemplateService ChecklistTemplates(TestDb t) => new(t.Db, new ChecklistTemplateRequestValidator());
    public static OccurrenceService Occurrences(TestDb t) => new(
        t.Db, t.Clock, t.CurrentUser, Files(t), Events(t), new OccurrenceListRequestValidator(),
        new OccurrenceRequestValidator(t.Clock), new OccurrenceStatusRequestValidator());
    public static ChecklistService Checklists(TestDb t) => new(
        t.Db, t.Clock, Files(t), Mileage(t), Occurrences(t), Events(t), new ChecklistListRequestValidator(), new ChecklistExecutionRequestValidator());

    // Phase 3
    public static WorkshopService Workshops(TestDb t) => new(t.Db, new WorkshopRequestValidator());
    public static MaintenancePlanService MaintenancePlans(TestDb t) => new(t.Db, new MaintenancePlanRequestValidator());
    public static MaintenanceScheduleService MaintenanceSchedules(TestDb t) => new(t.Db, t.Clock);
    public static HourMeterService HourMeter(TestDb t) => new(
        t.Db, t.Clock, t.CurrentUser, Events(t), new HourMeterReadingRequestValidator(t.Clock), new HourMeterReviewRequestValidator());
    public static WorkOrderService WorkOrders(TestDb t) => new(
        t.Db, t.Clock, t.CurrentUser, Events(t), MaintenanceSchedules(t), new WorkOrderRequestValidator(),
        new WorkOrderStatusRequestValidator(), new WorkOrderPartRequestValidator(), new WorkOrderLaborRequestValidator());
    public static MaintenanceRequestService MaintenanceRequests(TestDb t) => new(
        t.Db, t.Clock, t.CurrentUser, Events(t), WorkOrders(t), new MaintenanceRequestRequestValidator(t.Clock),
        new MaintenanceRequestRejectRequestValidator());

    // Phase 4
    public static FuelTypeService FuelTypes(TestDb t) => new(t.Db, t.CurrentUser, new FuelTypeRequestValidator());
    public static FuelStationService FuelStations(TestDb t) => new(
        t.Db, Events(t), new FuelStationRequestValidator(), new FuelPriceRequestValidator(t.Clock));
    public static FuelSettingsService FuelSettings(TestDb t) => new(t.Db, new FuelSettingsRequestValidator());
    public static FuelConsumptionService FuelConsumption(TestDb t) => new(t.Db, t.Clock, Events(t));
    public static FuelingService Fuelings(TestDb t) => new(
        t.Db, t.Clock, t.CurrentUser, Mileage(t), Files(t), FuelConsumption(t), FuelSettings(t), Events(t),
        new FuelingRequestValidator(t.Clock), new FuelingCorrectionRequestValidator(t.Clock), new FuelingListRequestValidator());
    public static FuelAnalyticsService FuelAnalytics(TestDb t) => new(
        t.Db, t.Clock, t.CurrentUser, Fuelings(t), new FuelPeriodRequestValidator());

    public static UserService Users(TestDb t) => new(
        t.Db, t.CurrentUser, Hasher, t.Clock, new UserCreateRequestValidator(), new UserUpdateRequestValidator());

    public static AuthService Auth(TestDb t, AuthOptions? options = null) => new(
        t.Db, Hasher, Tokens(t), t.Clock, t.CurrentUser, Options.Create(options ?? new AuthOptions()),
        new LoginRequestValidator(), new ChangePasswordRequestValidator(), NullLogger<AuthService>.Instance);

    public static JwtTokenService Tokens(TestDb t) => new(
        Options.Create(new JwtOptions { SigningKey = "test-signing-key-with-at-least-32-bytes!!" }), t.Clock);
}
