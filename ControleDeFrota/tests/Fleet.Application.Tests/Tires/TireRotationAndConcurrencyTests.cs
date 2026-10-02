using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Tests.TestSupport;
using Fleet.Application.Tires;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fleet.Application.Tests.Tires;

public class TireRotationAndConcurrencyTests : TireTestBase
{
    /// <summary>Four tires on the steer axle and the left side of the drive axle: A=1E, B=1D, C=2EE, D=2DE.</summary>
    private async Task<(Guid A, Guid B, Guid C, Guid D)> FourTiresAsync()
    {
        var a = await NewTireAsync("A");
        var b = await NewTireAsync("B");
        var c = await NewTireAsync("C");
        var d = await NewTireAsync("D");
        await InstallAsync(a.Id, "1E");
        await InstallAsync(b.Id, "1D");
        await InstallAsync(c.Id, "2EE");
        await InstallAsync(d.Id, "2DE");
        return (a.Id, b.Id, c.Id, d.Id);
    }

    private async Task<Dictionary<string, string>> PositionsAsync()
    {
        await using var db = T.NewContext();
        return await db.TireInstallations.Where(i => i.RemovedAt == null && i.VehicleId == VehicleId)
            .Select(i => new { i.PositionCode, i.Tire.Code }).ToDictionaryAsync(i => i.PositionCode, i => i.Code);
    }

    private TireRotationRequest Cross(Guid a, Guid b, Guid c, Guid d) => new()
    {
        VehicleId = VehicleId, Reason = "Rodízio preventivo",
        Moves =
        [
            new TireRotationMove { TireId = a, ToPositionCode = "2EE" },
            new TireRotationMove { TireId = b, ToPositionCode = "2DE" },
            new TireRotationMove { TireId = c, ToPositionCode = "1E" },
            new TireRotationMove { TireId = d, ToPositionCode = "1D" },
        ],
    };

    [Fact]
    public async Task Rotate_FourTires_MovesAllAtOnceAndKeepsTheHistory()
    {
        await ArrangeAsync();
        var (a, b, c, d) = await FourTiresAsync();
        await DriveToAsync(110_000);

        var result = await Operations.RotateAsync(Cross(a, b, c, d), default);

        (await PositionsAsync()).Should().BeEquivalentTo(new Dictionary<string, string> { ["2EE"] = "A", ["2DE"] = "B", ["1E"] = "C", ["1D"] = "D" });
        result.Positions.Single(p => p.Position.Code == "1E").Tire!.Code.Should().Be("C");
        var rotation = await T.Db.TireRotations.SingleAsync();
        rotation.TireCount.Should().Be(4);
        rotation.OdometerKm.Should().Be(110_000);
        var closed = await T.Db.TireInstallations.Where(i => i.RemovalRotationId == rotation.Id).ToListAsync();
        closed.Should().HaveCount(4).And.OnlyContain(i => i.DistanceKm == 10_000 && i.RemovalReason == TireRemovalReason.Rotation);
        (await T.Db.TireInstallations.CountAsync(i => i.RotationId == rotation.Id && i.RemovedAt == null)).Should().Be(4);
        (await Tires.GetAsync(a, default)).AccumulatedKm.Should().Be(10_000, "the km before the rotation stays with the tire");
        (await T.Db.OperationalEvents.CountAsync(e => e.Type == OperationalEventType.TireRotated && e.TireId != null)).Should().Be(4);
        (await T.Db.OperationalEvents.CountAsync(e => e.Type == OperationalEventType.TireRotated && e.VehicleId == VehicleId)).Should().Be(1,
            "one entry in the vehicle timeline for the whole rotation");
    }

    [Fact]
    public async Task Rotate_IntoAnEmptyPosition_IsAllowed()
    {
        await ArrangeAsync();
        var spare = await NewTireAsync("S");
        await InstallAsync(spare.Id, "EST1");

        await Operations.RotateAsync(new TireRotationRequest
        {
            VehicleId = VehicleId, Moves = [new TireRotationMove { TireId = spare.Id, ToPositionCode = "2EI" }],
        }, default);

        (await PositionsAsync()).Should().BeEquivalentTo(new Dictionary<string, string> { ["2EI"] = "S" });
    }

    [Fact]
    public async Task Rotate_TargetHeldByATireOutsideTheRotation_ConflictAndNothingChanges()
    {
        await ArrangeAsync();
        var (a, _, _, _) = await FourTiresAsync();

        var act = () => Operations.RotateAsync(new TireRotationRequest
        {
            VehicleId = VehicleId, Moves = [new TireRotationMove { TireId = a, ToPositionCode = "1D" }],
        }, default);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("não faz parte do rodízio");
        (await PositionsAsync())["1E"].Should().Be("A");
        (await T.Db.TireRotations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Rotate_PositionNotInLayout_ValidationError()
    {
        await ArrangeAsync();
        var (a, _, _, _) = await FourTiresAsync();

        var act = () => Operations.RotateAsync(new TireRotationRequest
        {
            VehicleId = VehicleId, Moves = [new TireRotationMove { TireId = a, ToPositionCode = "9EE" }],
        }, default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("moves[0].toPositionCode");
    }

    [Fact]
    public async Task Rotate_TwoTiresToTheSamePosition_ValidationError()
    {
        await ArrangeAsync();
        var (a, b, _, _) = await FourTiresAsync();

        var act = () => Operations.RotateAsync(new TireRotationRequest
        {
            VehicleId = VehicleId,
            Moves = [new TireRotationMove { TireId = a, ToPositionCode = "2EI" }, new TireRotationMove { TireId = b, ToPositionCode = "2EI" }],
        }, default);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*mesma posição*");
    }

    [Fact]
    public async Task Rotate_TireOfAnotherVehicle_ValidationError()
    {
        await ArrangeAsync();
        var outside = await NewTireAsync("X");

        var act = () => Operations.RotateAsync(new TireRotationRequest
        {
            VehicleId = VehicleId, Moves = [new TireRotationMove { TireId = outside.Id, ToPositionCode = "1E" }],
        }, default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage.Should().Contain("não está instalado");
    }

    /// <summary>Seção 51: a failure after the positions were vacated rolls everything back — no half rotation.</summary>
    [Fact]
    public async Task Rotate_FailureInTheMiddle_RollsBackTheWholeOperation()
    {
        await ArrangeAsync();
        var (a, b, c, d) = await FourTiresAsync();
        var before = await PositionsAsync();
        await using var failing = T.NewContext(new FailOnSaveInterceptor(failOn: 2));

        var act = () => TireServices.Operations(T, failing).RotateAsync(Cross(a, b, c, d), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Falha simulada*");
        (await PositionsAsync()).Should().BeEquivalentTo(before);
        await using var check = T.NewContext();
        (await check.TireRotations.CountAsync()).Should().Be(0);
        (await check.TireInstallations.CountAsync(i => i.RemovedAt != null)).Should().Be(0, "the stints closed by the first save were rolled back");
        (await check.Tires.SingleAsync(t => t.Id == a)).AccumulatedKm.Should().Be(0);
    }

    /// <summary>Seção 52: two users install the same tire at once — the second one gets a 409, never a tire on two vehicles.</summary>
    [Fact]
    public async Task Install_SameTireConcurrently_SecondOneConflicts()
    {
        await ArrangeAsync();
        var other = await Scenario.VehicleAsync(T, 1);
        await Operations.SetLayoutAsync(other.Id, null, new TireLayoutAssignmentRequest { LayoutId = LayoutId }, default);
        var tire = await NewTireAsync();

        // The second request read the tire (in stock) before the first one saved.
        await using var secondRequest = T.NewContext();
        (await secondRequest.Tires.SingleAsync(t => t.Id == tire.Id)).Status.Should().Be(TireStatus.InStock);
        await using var firstRequest = T.NewContext();
        await TireServices.Operations(T, firstRequest).InstallAsync(tire.Id, new TireInstallRequest { VehicleId = VehicleId, PositionCode = "1E" }, default);

        var act = () => TireServices.Operations(T, secondRequest)
            .InstallAsync(tire.Id, new TireInstallRequest { VehicleId = other.Id, PositionCode = "1E" }, default);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Be(TireLifecycle.ConcurrencyMessage);
        await using var check = T.NewContext();
        (await check.TireInstallations.CountAsync(i => i.TireId == tire.Id && i.RemovedAt == null)).Should().Be(1);
    }

    /// <summary>Even code that bypassed the service could not put one tire in two places: the database refuses it.</summary>
    [Fact]
    public async Task Database_RefusesTwoOpenInstallationsOfTheSameTire()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        T.Db.TireInstallations.AddRange(
            new TireInstallation { TireId = tire.Id, VehicleId = VehicleId, PositionCode = "1E", PositionLabel = "x", InstalledAt = T.Clock.UtcNow },
            new TireInstallation { TireId = tire.Id, VehicleId = VehicleId, PositionCode = "1D", PositionLabel = "x", InstalledAt = T.Clock.UtcNow });

        var act = () => T.Db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Database_RefusesTwoTiresInTheSamePosition()
    {
        await ArrangeAsync();
        var first = await NewTireAsync();
        var second = await NewTireAsync();
        T.Db.TireInstallations.AddRange(
            new TireInstallation { TireId = first.Id, VehicleId = VehicleId, PositionCode = "1E", PositionLabel = "x", InstalledAt = T.Clock.UtcNow },
            new TireInstallation { TireId = second.Id, VehicleId = VehicleId, PositionCode = "1E", PositionLabel = "x", InstalledAt = T.Clock.UtcNow });

        var act = () => T.Db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private sealed class FailOnSaveInterceptor(int failOn) : SaveChangesInterceptor
    {
        private int _count;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ++_count == failOn ? throw new InvalidOperationException("Falha simulada no meio da operação.") : base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
