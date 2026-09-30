using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Fleet.Api.Tests;

/// <summary>Phase 2 over the real HTTP pipeline: permissions per role, error contract, uploads and tenant isolation.</summary>
public class OperationsApiTests(FleetApiFactory factory) : IClassFixture<FleetApiFactory>
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>The fixture database is shared by the tests of this class, so each test registers its own records.</summary>
    private static async Task<Guid> CreateVehicleAsync(HttpClient admin, string plate, string renavam, string chassis)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/vehicles", new
        {
            licensePlate = plate, renavam, chassis, manufacturer = "Volvo", model = "FH 540", manufacturingYear = 2022, modelYear = 2022,
            type = "TruckTractor", fuelType = "DieselS10", currentOdometerKm = 10_000, status = "Available",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateDriverAsync(HttpClient admin, string cpf, string license)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/drivers", new
        {
            fullName = "Carlos Souza", cpf, birthDate = "1985-05-20", licenseNumber = license, licenseCategory = "E",
            licenseExpiresOn = "2030-01-01", status = "Active",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static object Occurrence(Guid vehicleId) => new
    {
        vehicleId, type = "VehicleDamage", severity = "Medium", description = "Retrovisor quebrado",
    };

    [Fact]
    public async Task Occurrences_ViewerAndMaintenanceCanRead_OnlyAllowedRolesCanCreate()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "OCC1A01", "55500000015", "9BWZZZ377VT000011");
        (await admin.PostAsJsonAsync("/api/v1/occurrences", Occurrence(vehicleId))).StatusCode.Should().Be(HttpStatusCode.Created);

        var viewer = await factory.CreateSignedInClientAsync(FleetApiFactory.ViewerA);
        (await viewer.GetAsync("/api/v1/occurrences")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.PostAsJsonAsync("/api/v1/occurrences", Occurrence(vehicleId))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var maintenance = await factory.CreateSignedInClientAsync(FleetApiFactory.MaintenanceA);
        (await maintenance.GetAsync("/api/v1/occurrences?openOnly=true")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await maintenance.PostAsJsonAsync("/api/v1/occurrences", Occurrence(vehicleId))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var operations = await factory.CreateSignedInClientAsync(FleetApiFactory.OperationsA);
        (await operations.PostAsJsonAsync("/api/v1/occurrences", Occurrence(vehicleId))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task DriverRole_HasNoOperationalAccessInThisPhase()
    {
        var driver = await factory.CreateSignedInClientAsync(FleetApiFactory.DriverA);

        (await driver.GetAsync("/api/v1/checklists")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await driver.GetAsync("/api/v1/vehicles")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await driver.PostAsJsonAsync("/api/v1/checklists", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Mileage_OperationsRecordsReadings_ButCannotCorrect()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "KMS1A02", "55500000023", "9BWZZZ377VT000022");
        var operations = await factory.CreateSignedInClientAsync(FleetApiFactory.OperationsA);

        var reading = await operations.PostAsJsonAsync($"/api/v1/vehicles/{vehicleId}/odometer-readings", new { odometerKm = 10_300 });
        reading.StatusCode.Should().Be(HttpStatusCode.Created);

        var correction = await operations.PostAsJsonAsync($"/api/v1/vehicles/{vehicleId}/odometer-readings",
            new { odometerKm = 9_000, isCorrection = true, notes = "erro" });
        correction.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(correction)).GetProperty("title").GetString().Should().Contain("gestores");

        var decrease = await operations.PostAsJsonAsync($"/api/v1/vehicles/{vehicleId}/odometer-readings", new { odometerKm = 9_000 });
        decrease.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(decrease)).GetProperty("errors").GetProperty("odometerKm")[0].GetString().Should().Contain("10.300 km");
    }

    [Fact]
    public async Task Assignment_Conflict_Returns409WithActionableMessage_AndDashboardShowsAssigned()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "ASG1A03", "55500000031", "9BWZZZ377VT000033");
        var first = await CreateDriverAsync(admin, "24681357928", "11122233344");
        var second = await CreateDriverAsync(admin, "13579246828", "22233344455");

        (await admin.PostAsJsonAsync($"/api/v1/vehicles/{vehicleId}/assignments", new { driverId = first })).StatusCode.Should().Be(HttpStatusCode.Created);
        var conflict = await admin.PostAsJsonAsync($"/api/v1/vehicles/{vehicleId}/assignments", new { driverId = second });

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(conflict)).GetProperty("title").GetString().Should().Contain("Confirme a troca");
        var vehicle = await JsonAsync(await admin.GetAsync($"/api/v1/vehicles/{vehicleId}"));
        vehicle.GetProperty("operationalStatus").GetString().Should().Be("Assigned");
        var dashboard = await JsonAsync(await admin.GetAsync("/api/v1/dashboard"));
        dashboard.GetProperty("fleet").GetProperty("assigned").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var history = await JsonAsync(await admin.GetAsync($"/api/v1/vehicles/{vehicleId}/history"));
        history.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("type").GetString()).Should().Contain("VehicleAssigned");
    }

    [Fact]
    public async Task Files_UploadValidatesContent_AndDownloadFollowsOwnership()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var pdf = "%PDF-1.7 teste"u8.ToArray();

        var upload = await admin.PostAsync("/api/v1/files", Multipart(pdf, "crlv.pdf", "application/pdf"));
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var fileId = (await JsonAsync(upload)).GetProperty("id").GetGuid();

        var download = await admin.GetAsync($"/api/v1/files/{fileId}");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(pdf);

        // Not attached yet: only the uploader may open it, even with documents.view.
        var viewer = await factory.CreateSignedInClientAsync(FleetApiFactory.ViewerA);
        (await viewer.GetAsync($"/api/v1/files/{fileId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var otherTenant = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminB);
        (await otherTenant.GetAsync($"/api/v1/files/{fileId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var disguised = await admin.PostAsync("/api/v1/files", Multipart("MZ\u0090 not a pdf"u8.ToArray(), "nota.pdf", "application/pdf"));
        disguised.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(disguised)).GetProperty("errors").GetProperty("file")[0].GetString().Should().Contain("PDF, JPG ou PNG");
    }

    [Fact]
    public async Task Documents_CrossTenantAccess_Returns404()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "DOC1A04", "55500000040", "9BWZZZ377VT000044");
        var types = await JsonAsync(await admin.GetAsync("/api/v1/document-types?ownerType=Vehicle"));
        var insurance = types.EnumerateArray().Single(t => t.GetProperty("name").GetString() == "Seguro").GetProperty("id").GetGuid();
        var created = await admin.PostAsJsonAsync("/api/v1/documents", new { documentTypeId = insurance, ownerId = vehicleId, expiresOn = "2030-01-01" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var documentId = (await JsonAsync(created)).GetProperty("id").GetGuid();

        var otherTenant = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminB);
        (await otherTenant.GetAsync($"/api/v1/documents/{documentId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var operations = await factory.CreateSignedInClientAsync(FleetApiFactory.OperationsA);
        (await operations.DeleteAsync($"/api/v1/documents/{documentId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "operations cannot delete documents");
        (await operations.PostAsJsonAsync("/api/v1/document-types", new { name = "Novo", ownerType = "Vehicle" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "configuration needs operations.configure");
    }

    [Fact]
    public async Task AuditHistory_CoversOperationalEntities()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "AUD1A05", "55500000058", "9BWZZZ377VT000055");
        var occurrence = await JsonAsync(await admin.PostAsJsonAsync("/api/v1/occurrences", Occurrence(vehicleId)));
        var id = occurrence.GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/occurrences/{id}/status", new { status = "Cancelled", resolution = "Duplicada" });

        var audit = await JsonAsync(await admin.GetAsync($"/api/v1/audit/Occurrence/{id}"));

        audit.EnumerateArray().Select(e => e.GetProperty("action").GetString()).Should().Equal("Updated", "Created");
    }

    private static MultipartFormDataContent Multipart(byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}
