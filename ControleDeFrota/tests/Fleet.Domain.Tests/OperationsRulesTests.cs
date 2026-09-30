using FluentAssertions;
using Fleet.Domain.Assignments;
using Fleet.Domain.Checklists;
using Fleet.Domain.Documents;
using Fleet.Domain.Drivers;
using Fleet.Domain.Files;
using Fleet.Domain.Mileage;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Tests;

public class VehicleOperationalStateTests
{
    [Theory]
    [InlineData(VehicleStatus.Available, false, VehicleOperationalStatus.Available)]
    [InlineData(VehicleStatus.Available, true, VehicleOperationalStatus.Assigned)]
    [InlineData(VehicleStatus.OnTrip, true, VehicleOperationalStatus.OnTrip)]
    [InlineData(VehicleStatus.UnderMaintenance, true, VehicleOperationalStatus.UnderMaintenance)]
    [InlineData(VehicleStatus.Unavailable, true, VehicleOperationalStatus.Unavailable)]
    [InlineData(VehicleStatus.Inactive, false, VehicleOperationalStatus.Inactive)]
    public void From_StoredConditionWinsOverAssignment(VehicleStatus status, bool assigned, VehicleOperationalStatus expected) =>
        VehicleOperationalState.From(status, assigned).Should().Be(expected);
}

public class AssignmentRulesTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);

    private static Driver Driver(DriverStatus status = DriverStatus.Active, int licenseDays = 365) =>
        new() { FullName = "João", Status = status, LicenseExpiresOn = Today.AddDays(licenseDays) };

    [Fact]
    public void DriverBlockReason_ActiveWithValidLicense_IsNull() => AssignmentRules.DriverBlockReason(Driver(), Today).Should().BeNull();

    [Theory]
    [InlineData(DriverStatus.Inactive, 365, "desligado")]
    [InlineData(DriverStatus.OnLeave, 365, "afastado")]
    [InlineData(DriverStatus.Active, -1, "CNH")]
    public void DriverBlockReason_BlockedDriver_ExplainsWhy(DriverStatus status, int licenseDays, string expected) =>
        AssignmentRules.DriverBlockReason(Driver(status, licenseDays), Today).Should().Contain(expected);

    [Fact]
    public void DriverBlockReason_LicenseExpiringToday_IsStillAllowed() =>
        AssignmentRules.DriverBlockReason(Driver(licenseDays: 0), Today).Should().BeNull();

    [Theory]
    [InlineData(VehicleStatus.Inactive, true)]
    [InlineData(VehicleStatus.UnderMaintenance, false)]
    [InlineData(VehicleStatus.Unavailable, false)]
    [InlineData(VehicleStatus.Available, false)]
    public void VehicleBlockReason_OnlyInactiveIsBlocked(VehicleStatus status, bool blocked) =>
        (AssignmentRules.VehicleBlockReason(new Vehicle { Status = status }) is not null).Should().Be(blocked);
}

public class OdometerPolicyTests
{
    private static readonly DateTime Base = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly OdometerBaseline Baseline = new(120_000, Base);

    [Fact]
    public void Evaluate_NoBaseline_IsValid() => OdometerPolicy.Evaluate(5, Base, null).Check.Should().Be(OdometerCheck.Valid);

    [Theory]
    [InlineData(120_000, 1)]   // did not move
    [InlineData(120_480, 4)]
    [InlineData(121_500, 1)]   // exactly the plausible limit in one day
    [InlineData(127_000, 5)]   // 1,400 km/day
    public void Evaluate_PlausibleIncrease_IsValid(int km, int days) =>
        OdometerPolicy.Evaluate(km, Base.AddDays(days), Baseline).Check.Should().Be(OdometerCheck.Valid);

    [Fact]
    public void Evaluate_LowerThanBaseline_IsDecrease() =>
        OdometerPolicy.Evaluate(119_999, Base.AddDays(1), Baseline).Check.Should().Be(OdometerCheck.Decrease);

    [Fact]
    public void Evaluate_ExtraDigitTypo_IsSuspiciousWithExplanation()
    {
        var result = OdometerPolicy.Evaluate(1_204_800, Base.AddDays(4), Baseline);

        result.Check.Should().Be(OdometerCheck.SuspiciousJump);
        result.Anomaly.Should().Contain("1.084.800 km").And.Contain("4 dias").And.Contain("1.500 km/dia");
    }

    [Fact]
    public void Evaluate_SameDayJump_CountsAtLeastOneDay() =>
        OdometerPolicy.Evaluate(121_501, Base.AddMinutes(10), Baseline).Check.Should().Be(OdometerCheck.SuspiciousJump);
}

public class DocumentExpiryPolicyTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);

    [Theory]
    [InlineData(-1, DocumentStatus.Expired)]
    [InlineData(0, DocumentStatus.ExpiringSoon)]   // expires today: still valid today, but alert
    [InlineData(30, DocumentStatus.ExpiringSoon)]  // window limit
    [InlineData(31, DocumentStatus.Valid)]
    public void Evaluate_ByDaysUntilExpiration(int days, DocumentStatus expected) =>
        DocumentExpiryPolicy.Evaluate(Today.AddDays(days), alertDaysBefore: 30, Today).Should().Be(expected);

    [Fact]
    public void Evaluate_ThresholdIsPerType() =>
        DocumentExpiryPolicy.Evaluate(Today.AddDays(40), alertDaysBefore: 45, Today).Should().Be(DocumentStatus.ExpiringSoon);

    [Fact]
    public void Evaluate_NoDate_IsNoExpiration() =>
        DocumentExpiryPolicy.Evaluate(null, 30, Today).Should().Be(DocumentStatus.NoExpiration);

    [Fact]
    public void Evaluate_TypeWithoutExpiration_IsNoExpirationEvenWithDate()
    {
        var alertStarts = DocumentExpiryPolicy.AlertStartsOn(Today.AddDays(-5), typeHasExpiration: false, 30);

        alertStarts.Should().BeNull();
        DocumentExpiryPolicy.Evaluate(Today.AddDays(-5), alertStarts, Today, replaced: false).Should().Be(DocumentStatus.NoExpiration);
    }

    [Fact]
    public void Evaluate_Replaced_WinsOverExpired() =>
        DocumentExpiryPolicy.Evaluate(Today.AddDays(-10), 30, Today, replaced: true).Should().Be(DocumentStatus.Replaced);

    [Fact]
    public void DaysUntilExpiration_IsNegativeAfterExpiry() =>
        DocumentExpiryPolicy.DaysUntilExpiration(Today.AddDays(-3), Today).Should().Be(-3);
}

public class OccurrenceWorkflowTests
{
    [Theory]
    [InlineData(OccurrenceStatus.Open, OccurrenceStatus.InAnalysis, true)]
    [InlineData(OccurrenceStatus.Open, OccurrenceStatus.Resolved, true)]
    [InlineData(OccurrenceStatus.Open, OccurrenceStatus.Cancelled, true)]
    [InlineData(OccurrenceStatus.InAnalysis, OccurrenceStatus.Resolved, true)]
    [InlineData(OccurrenceStatus.InAnalysis, OccurrenceStatus.Cancelled, true)]
    [InlineData(OccurrenceStatus.InAnalysis, OccurrenceStatus.Open, false)]
    [InlineData(OccurrenceStatus.Resolved, OccurrenceStatus.Open, false)]
    [InlineData(OccurrenceStatus.Resolved, OccurrenceStatus.Cancelled, false)]
    [InlineData(OccurrenceStatus.Cancelled, OccurrenceStatus.Open, false)]
    [InlineData(OccurrenceStatus.Open, OccurrenceStatus.Open, false)]
    public void CanTransition_FollowsTheExplicitMachine(OccurrenceStatus from, OccurrenceStatus to, bool allowed) =>
        OccurrenceWorkflow.CanTransition(from, to).Should().Be(allowed);

    [Theory]
    [InlineData(OccurrenceStatus.Resolved, true)]
    [InlineData(OccurrenceStatus.Cancelled, true)]
    [InlineData(OccurrenceStatus.InAnalysis, false)]
    public void RequiresResolution_OnlyWhenClosing(OccurrenceStatus to, bool required) =>
        OccurrenceWorkflow.RequiresResolution(to).Should().Be(required);
}

public class ChecklistScheduleTests
{
    [Theory]
    [InlineData("2026-09-30", "2026-09-28")] // Wednesday → Monday
    [InlineData("2026-09-28", "2026-09-28")] // Monday
    [InlineData("2026-10-04", "2026-09-28")] // Sunday belongs to the week that started on Monday
    public void PeriodStart_WeeklyStartsOnMonday(string today, string expected) =>
        ChecklistSchedule.PeriodStart(ChecklistFrequency.Weekly, DateOnly.Parse(today)).Should().Be(DateOnly.Parse(expected));

    [Fact]
    public void PeriodStart_DailyIsToday() =>
        ChecklistSchedule.PeriodStart(ChecklistFrequency.Daily, new DateOnly(2026, 9, 30)).Should().Be(new DateOnly(2026, 9, 30));

    [Theory]
    [InlineData(VehicleStatus.Available, true, true)]
    [InlineData(VehicleStatus.OnTrip, true, true)]
    [InlineData(VehicleStatus.Available, false, false)] // pool vehicle without driver
    [InlineData(VehicleStatus.UnderMaintenance, true, false)]
    public void IsExpectedFor_VehiclesInOperation(VehicleStatus status, bool assigned, bool expected) =>
        ChecklistSchedule.IsExpectedFor(status, assigned).Should().Be(expected);
}

public class FileRulesTests
{
    [Fact]
    public void DetectContentType_RecognizesSignatures()
    {
        FileRules.DetectContentType("%PDF-1.7"u8).Should().Be(FileRules.Pdf);
        FileRules.DetectContentType([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0]).Should().Be(FileRules.Jpeg);
        FileRules.DetectContentType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]).Should().Be(FileRules.Png);
    }

    [Fact]
    public void DetectContentType_ExecutableRenamedToPdf_IsRejected() =>
        FileRules.DetectContentType("MZ\u0090\0\u0003\0\0\0"u8).Should().BeNull();

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData(@"C:\fotos\pneu.jpg", "pneu.jpg")]
    [InlineData("nota<script>.pdf", "notascript.pdf")]
    [InlineData("...", "arquivo.pdf")]
    [InlineData(null, "arquivo.pdf")]
    public void SanitizeFileName_KeepsOnlyASafeBaseName(string? input, string expected) =>
        FileRules.SanitizeFileName(input, FileRules.Pdf).Should().Be(expected);
}

public class LicensePlateFormatTests
{
    [Theory]
    [InlineData("ABC1234", "ABC-1234")]
    [InlineData("abc1d23", "ABC1D23")]
    public void Format_LegacyGetsHyphenMercosulDoesNot(string plate, string expected) => LicensePlate.Format(plate).Should().Be(expected);
}
