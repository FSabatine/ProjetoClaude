using Fleet.Application.Common;
using Fleet.Application.Companies;
using Fleet.Application.Drivers;
using Fleet.Application.Implements;
using Fleet.Application.Maintenance;
using Fleet.Application.Vehicles;
using Fleet.Domain.Drivers;
using Fleet.Domain.Implements;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Vehicles;

namespace Fleet.Application.Tests.TestSupport;

/// <summary>Valid requests; tests override only what they exercise (with { ... }).</summary>
public static class Requests
{
    public static VehicleRequest Vehicle(string plate = "ABC1D23", string renavam = "12345678900", string chassis = "9BWZZZ377VT004251") => new()
    {
        LicensePlate = plate,
        Renavam = renavam,
        Chassis = chassis,
        Manufacturer = "Scania",
        Model = "R 450",
        ManufacturingYear = 2022,
        ModelYear = 2023,
        Type = VehicleType.TruckTractor,
        FuelType = FuelType.DieselS10,
        CurrentOdometerKm = 1000,
        Status = VehicleStatus.Available,
    };

    public static ImplementRequest Implement(string plate = "IMP1A23", string renavam = "00123456789", string chassis = "9EP07142RAA000123") => new()
    {
        LicensePlate = plate,
        Renavam = renavam,
        Chassis = chassis,
        Manufacturer = "Randon",
        Model = "Graneleiro",
        ManufacturingYear = 2022,
        ModelYear = 2022,
        Type = ImplementType.SemiTrailer,
        Capacity = 32000,
        CapacityUnit = CapacityUnit.Kg,
    };

    public static DriverRequest Driver(DateOnly today, string cpf = "529.982.247-25", string license = "04512345678") => new()
    {
        FullName = "João da Silva",
        Cpf = cpf,
        BirthDate = today.AddYears(-30),
        LicenseNumber = license,
        LicenseCategory = LicenseCategory.E,
        LicenseExpiresOn = today.AddYears(2),
        Status = DriverStatus.Active,
    };

    public static WorkshopRequest Workshop(string name = "Oficina Central") => new() { Name = name, Status = WorkshopStatus.Active };

    public static MaintenancePlanRequest MaintenancePlan(Guid? vehicleId = null, VehicleType? vehicleType = null) => new()
    {
        Name = "Plano padrão",
        VehicleId = vehicleId,
        VehicleType = vehicleId is null ? vehicleType : null,
        IsActive = true,
        Items =
        [
            new MaintenancePlanItemRequest { ServiceName = "Troca de óleo", IntervalKm = 10_000, GraceKm = 500, Priority = MaintenancePriority.High },
        ],
    };

    public static WorkOrderRequest WorkOrder(Guid vehicleId) => new()
    {
        VehicleId = vehicleId,
        Type = MaintenanceType.Corrective,
        Priority = MaintenancePriority.Medium,
        Description = "Barulho no motor",
    };

    public static MaintenanceRequestRequest MaintenanceRequest(Guid vehicleId) => new()
    {
        VehicleId = vehicleId,
        Source = MaintenanceRequestSource.FleetManager,
        MaintenanceType = MaintenanceType.Corrective,
        Priority = MaintenancePriority.Medium,
        Description = "Barulho no motor",
    };

    public static CompanyRequest Company(string cnpj = "12.ABC.345/01DE-35") => new()
    {
        LegalName = "Nova Transportes Ltda",
        Cnpj = cnpj,
        Email = "contato@nova.com.br",
        Address = new AddressDto
        {
            Street = "Rua B", Number = "200", Neighborhood = "Centro", City = "Londrina", State = "pr", ZipCode = "86010-000",
        },
    };
}
