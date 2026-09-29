using Fleet.Application.Auth;
using Fleet.Application.Companies;
using Fleet.Application.Dashboard;
using Fleet.Application.Drivers;
using Fleet.Application.Implements;
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

    public static VehicleService Vehicles(TestDb t) => new(t.Db, new VehicleRequestValidator(t.Clock));
    public static ImplementService Implements(TestDb t) => new(t.Db, new ImplementRequestValidator(t.Clock));
    public static DriverService Drivers(TestDb t) => new(t.Db, t.Clock, new DriverRequestValidator(t.Clock));
    public static CompanyService Companies(TestDb t) => new(t.Db, t.CurrentUser, new CompanyRequestValidator());
    public static DashboardService Dashboard(TestDb t) => new(t.Db, t.Clock, t.CurrentUser);

    public static UserService Users(TestDb t) => new(
        t.Db, t.CurrentUser, Hasher, t.Clock, new UserCreateRequestValidator(), new UserUpdateRequestValidator());

    public static AuthService Auth(TestDb t, AuthOptions? options = null) => new(
        t.Db, Hasher, Tokens(t), t.Clock, t.CurrentUser, Options.Create(options ?? new AuthOptions()),
        new LoginRequestValidator(), new ChangePasswordRequestValidator(), NullLogger<AuthService>.Instance);

    public static JwtTokenService Tokens(TestDb t) => new(
        Options.Create(new JwtOptions { SigningKey = "test-signing-key-with-at-least-32-bytes!!" }), t.Clock);
}
