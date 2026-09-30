using Fleet.Application.Drivers;
using Fleet.Application.Vehicles;
using Fleet.Domain.Authorization;
using Fleet.Domain.Companies;

namespace Fleet.Application.Tests.TestSupport;

/// <summary>Valid, distinct fleet records for operational tests (documents with correct check digits).</summary>
public static class Scenario
{
    private static readonly (string Plate, string Renavam, string Chassis)[] Vehicles =
    [
        ("ABC1D23", "12345678900", "9BWZZZ377VT004251"),
        ("DEF5G67", "00123456789", "9BSR6X400J1234567"),
        ("GHI8J90", "23456789013", "9BM958074AB123456"),
        ("JKL1M23", "34567890125", "9BVAG40D5KE765432"),
    ];

    private static readonly (string Cpf, string License)[] Drivers =
    [
        ("529.982.247-25", "04512345678"),
        ("123.456.789-09", "05623456789"),
        ("987.654.321-00", "06734567890"),
        ("111.444.777-35", "07845678901"),
    ];

    public static async Task<Company> SignedInAsync(TestDb t, string role = SystemRoles.FleetManager, string cnpj = "11222333000181")
    {
        var company = await t.AddCompanyAsync(cnpj);
        t.SignInAs(company, role);
        return company;
    }

    public static Task<VehicleResponse> VehicleAsync(TestDb t, int index = 0, Func<VehicleRequest, VehicleRequest>? change = null)
    {
        var (plate, renavam, chassis) = Vehicles[index];
        var request = Requests.Vehicle(plate, renavam, chassis);
        return Services.Vehicles(t).CreateAsync(change is null ? request : change(request), default);
    }

    public static Task<DriverResponse> DriverAsync(TestDb t, int index = 0, Func<DriverRequest, DriverRequest>? change = null)
    {
        var (cpf, license) = Drivers[index];
        var request = Requests.Driver(t.Clock.Today, cpf, license) with { FullName = $"Motorista {index + 1}" };
        return Services.Drivers(t).CreateAsync(change is null ? request : change(request), default);
    }
}
