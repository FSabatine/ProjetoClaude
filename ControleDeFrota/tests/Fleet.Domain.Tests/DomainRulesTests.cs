using FluentAssertions;
using Fleet.Domain.Authorization;
using Fleet.Domain.Drivers;
using Fleet.Domain.Users;

namespace Fleet.Domain.Tests;

public class DriverLicenseStateTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    [Theory]
    [InlineData(-1, true, false)]  // expired yesterday
    [InlineData(0, false, true)]   // expires today: still valid, alert
    [InlineData(30, false, true)]  // alert window limit
    [InlineData(31, false, false)] // outside the window
    public void LicenseState_DependsOnDaysUntilExpiry(int daysUntilExpiry, bool expired, bool expiringSoon)
    {
        var driver = new Driver { LicenseExpiresOn = Today.AddDays(daysUntilExpiry) };

        driver.IsLicenseExpired(Today).Should().Be(expired);
        driver.IsLicenseExpiringSoon(Today).Should().Be(expiringSoon);
    }
}

public class UserLockoutTests
{
    [Fact]
    public void IsLockedOut_LockoutInTheFuture_ReturnsTrue()
    {
        var now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
        new User { LockoutEndAt = now.AddMinutes(1) }.IsLockedOut(now).Should().BeTrue();
        new User { LockoutEndAt = now.AddMinutes(-1) }.IsLockedOut(now).Should().BeFalse();
        new User { LockoutEndAt = null }.IsLockedOut(now).Should().BeFalse();
    }
}

public class PermissionCatalogTests
{
    [Fact]
    public void Catalog_IdsAndKeysAreUnique()
    {
        PermissionCatalog.All.Select(p => p.Id).Should().OnlyHaveUniqueItems();
        PermissionCatalog.All.Select(p => p.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Catalog_ContainsEveryDeclaredPermissionConstant()
    {
        var declared = typeof(Permissions).GetNestedTypes()
            .SelectMany(t => t.GetFields())
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!);

        PermissionCatalog.All.Select(p => p.Key).Should().BeEquivalentTo(declared);
    }

    [Fact]
    public void Catalog_KeysFollowModuleDotActionConvention() =>
        PermissionCatalog.All.Should().OnlyContain(p => p.Key.Count(c => c == '.') == 1 && p.Key == p.Key.ToLowerInvariant());

    [Fact]
    public void SystemRoles_OnlyReferenceCatalogPermissions() =>
        SystemRoles.All.SelectMany(r => r.Permissions)
            .Should().OnlyContain(key => PermissionCatalog.All.Any(p => p.Key == key));

    [Fact]
    public void SystemRoles_OnlyPlatformAdministratorCanManageCompanies()
    {
        SystemRoles.All.Where(r => r.Permissions.Contains(Permissions.Companies.Manage))
            .Select(r => r.Key)
            .Should().Equal(SystemRoles.PlatformAdministrator);
    }

    [Fact]
    public void SystemRoles_AdministratorHasEverythingButPlatformManagement()
    {
        var admin = SystemRoles.All.Single(r => r.Key == SystemRoles.Administrator);
        admin.Permissions.Should().HaveCount(PermissionCatalog.All.Count - 1);
    }

    [Fact]
    public void SystemRoles_ViewerIsReadOnly() =>
        SystemRoles.All.Single(r => r.Key == SystemRoles.Viewer).Permissions
            .Should().OnlyContain(p => p.EndsWith(".view"));
}
