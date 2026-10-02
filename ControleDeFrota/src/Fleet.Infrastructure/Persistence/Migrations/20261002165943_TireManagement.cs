using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TireManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TireLayoutId",
                table: "Vehicles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TireId",
                table: "OperationalEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TireLayoutId",
                table: "Implements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TireLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Target = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SpareCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TireLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TireLayouts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TireModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Size = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Application = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Construction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LoadIndex = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SpeedRating = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    OriginalTreadDepthMm = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TireModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TireModels_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TireRotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImplementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OdometerKm = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TireCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TireRotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TireRotations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireRotations_Implements_ImplementId",
                        column: x => x.ImplementId,
                        principalTable: "Implements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireRotations_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TireSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinTreadDepthMm = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TreadWarningDepthMm = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    InspectionIntervalDays = table.Column<int>(type: "int", nullable: false),
                    MaxAgeYears = table.Column<int>(type: "int", nullable: false),
                    PressureTolerancePercent = table.Column<int>(type: "int", nullable: false),
                    PressureUnit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RapidWearMmPer1000Km = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MinExpectedLifeKm = table.Column<int>(type: "int", nullable: false),
                    AutoMaintenanceRequestOnUnfit = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TireSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TireSettings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TireLayoutAxles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TireLayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDual = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    AllowedSize = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    RecommendedPressurePsi = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TireLayoutAxles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TireLayoutAxles_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireLayoutAxles_TireLayouts_TireLayoutId",
                        column: x => x.TireLayoutId,
                        principalTable: "TireLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tires",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TireModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Dot = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ManufacturedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchasedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    Supplier = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    OriginalTreadDepthMm = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    StorageLocation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccumulatedKm = table.Column<int>(type: "int", nullable: false),
                    HasUnmeasuredDistance = table.Column<bool>(type: "bit", nullable: false),
                    RetreadCount = table.Column<int>(type: "int", nullable: false),
                    RepairCount = table.Column<int>(type: "int", nullable: false),
                    CurrentTreadDepthMm = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    TreadMeasuredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastInspectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastWearPattern = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LastInspectionHasDamage = table.Column<bool>(type: "bit", nullable: false),
                    LastPressureCheck = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InspectionReferenceAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMovementAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisposedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisposalReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DisposalDestination = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisposalNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisposedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tires", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tires_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tires_TireModels_TireModelId",
                        column: x => x.TireModelId,
                        principalTable: "TireModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TireAnomalies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TireId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TireAnomalies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TireAnomalies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireAnomalies_Tires_TireId",
                        column: x => x.TireId,
                        principalTable: "Tires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TireInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TireId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImplementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    PositionLabel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AxleNumber = table.Column<int>(type: "int", nullable: false),
                    IsSpare = table.Column<bool>(type: "bit", nullable: false),
                    InstalledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InstalledOdometerKm = table.Column<int>(type: "int", nullable: true),
                    InstalledHourMeter = table.Column<decimal>(type: "decimal(10,1)", precision: 10, scale: 1, nullable: true),
                    InstallReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovedOdometerKm = table.Column<int>(type: "int", nullable: true),
                    RemovedHourMeter = table.Column<decimal>(type: "decimal(10,1)", precision: 10, scale: 1, nullable: true),
                    RemovalReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RemovalDestination = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RemovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemovalRotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemovalNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DistanceKm = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TireInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TireInstallations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireInstallations_Implements_ImplementId",
                        column: x => x.ImplementId,
                        principalTable: "Implements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireInstallations_TireRotations_RemovalRotationId",
                        column: x => x.RemovalRotationId,
                        principalTable: "TireRotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireInstallations_TireRotations_RotationId",
                        column: x => x.RotationId,
                        principalTable: "TireRotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireInstallations_Tires_TireId",
                        column: x => x.TireId,
                        principalTable: "Tires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireInstallations_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TireServiceOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TireId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InPlace = table.Column<bool>(type: "bit", nullable: false),
                    WorkshopId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RepairType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RetreadNumber = table.Column<int>(type: "int", nullable: true),
                    TreadPattern = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    NewTreadDepthMm = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    WarrantyUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResultNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TireServiceOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TireServiceOrders_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireServiceOrders_Tires_TireId",
                        column: x => x.TireId,
                        principalTable: "Tires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireServiceOrders_Workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "Workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TireInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TireId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImplementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    PositionLabel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    InspectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OdometerKm = table.Column<int>(type: "int", nullable: true),
                    TireKm = table.Column<int>(type: "int", nullable: true),
                    TreadDepthMm = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Pressure = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    PressureUnit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PressureCheck = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WearPattern = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaintenanceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TireInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TireInspections_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireInspections_Implements_ImplementId",
                        column: x => x.ImplementId,
                        principalTable: "Implements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireInspections_TireInstallations_InstallationId",
                        column: x => x.InstallationId,
                        principalTable: "TireInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireInspections_Tires_TireId",
                        column: x => x.TireId,
                        principalTable: "Tires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireInspections_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TireCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TireId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IncurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ServiceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TireCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TireCosts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireCosts_TireServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "TireServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireCosts_Tires_TireId",
                        column: x => x.TireId,
                        principalTable: "Tires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TireInspectionDamages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TireInspectionDamages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TireInspectionDamages_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TireInspectionDamages_TireInspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "TireInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Module" },
                values: new object[,]
                {
                    { 180, "Visualizar pneus, histórico, painel e relatórios de pneus", "tires.view", "tires" },
                    { 181, "Cadastrar pneus", "tires.create", "tires" },
                    { 182, "Editar pneus e corrigir o histórico de pneus", "tires.edit", "tires" },
                    { 183, "Instalar, substituir e transferir pneus", "tires.install", "tires" },
                    { 184, "Remover pneus", "tires.remove", "tires" },
                    { 185, "Fazer rodízio de pneus", "tires.rotate", "tires" },
                    { 186, "Inspecionar pneus (sulco, pressão, danos)", "tires.inspect", "tires" },
                    { 187, "Registrar consertos de pneus", "tires.repair", "tires" },
                    { 188, "Registrar recapagens de pneus", "tires.retread", "tires" },
                    { 189, "Dar baixa em pneus", "tires.dispose", "tires" },
                    { 190, "Visualizar custos de pneus", "tires.viewcosts", "tires" },
                    { 191, "Configurar eixos, modelos e limites de pneus", "tires.managesettings", "tires" }
                });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "Gerencia a frota e a operação: cadastros, alocações, hodômetro, documentos, checklists, ocorrências, manutenção, combustível e pneus.");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "Description",
                value: "Operação diária: motoristas, alocações, hodômetro, documentos, checklists, ocorrências, abastecimentos e inspeção de pneus.");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "Description",
                value: "Atualiza a situação de veículos e implementos, acompanha ocorrências e executa a manutenção da frota, inclusive a borracharia.");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "Description",
                value: "Consulta a frota e os custos de manutenção, combustível e pneus.");

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 180, 1 },
                    { 181, 1 },
                    { 182, 1 },
                    { 183, 1 },
                    { 184, 1 },
                    { 185, 1 },
                    { 186, 1 },
                    { 187, 1 },
                    { 188, 1 },
                    { 189, 1 },
                    { 190, 1 },
                    { 191, 1 },
                    { 180, 2 },
                    { 181, 2 },
                    { 182, 2 },
                    { 183, 2 },
                    { 184, 2 },
                    { 185, 2 },
                    { 186, 2 },
                    { 187, 2 },
                    { 188, 2 },
                    { 189, 2 },
                    { 190, 2 },
                    { 191, 2 },
                    { 180, 3 },
                    { 181, 3 },
                    { 182, 3 },
                    { 183, 3 },
                    { 184, 3 },
                    { 185, 3 },
                    { 186, 3 },
                    { 187, 3 },
                    { 188, 3 },
                    { 189, 3 },
                    { 190, 3 },
                    { 191, 3 },
                    { 180, 4 },
                    { 186, 4 },
                    { 180, 5 },
                    { 181, 5 },
                    { 182, 5 },
                    { 183, 5 },
                    { 184, 5 },
                    { 185, 5 },
                    { 186, 5 },
                    { 187, 5 },
                    { 188, 5 },
                    { 189, 5 },
                    { 190, 5 },
                    { 191, 5 },
                    { 180, 6 },
                    { 190, 6 },
                    { 180, 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TireLayoutId",
                table: "Vehicles",
                column: "TireLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_CompanyId_TireId_OccurredAt",
                table: "OperationalEvents",
                columns: new[] { "CompanyId", "TireId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Implements_TireLayoutId",
                table: "Implements",
                column: "TireLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_TireAnomalies_CompanyId_ReviewedAt",
                table: "TireAnomalies",
                columns: new[] { "CompanyId", "ReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TireAnomalies_CompanyId_TireId",
                table: "TireAnomalies",
                columns: new[] { "CompanyId", "TireId" });

            migrationBuilder.CreateIndex(
                name: "IX_TireAnomalies_TireId",
                table: "TireAnomalies",
                column: "TireId");

            migrationBuilder.CreateIndex(
                name: "IX_TireCosts_CompanyId_IncurredOn",
                table: "TireCosts",
                columns: new[] { "CompanyId", "IncurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_TireCosts_CompanyId_TireId_Type",
                table: "TireCosts",
                columns: new[] { "CompanyId", "TireId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_TireCosts_ServiceOrderId",
                table: "TireCosts",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TireCosts_TireId",
                table: "TireCosts",
                column: "TireId");

            migrationBuilder.CreateIndex(
                name: "IX_TireInspectionDamages_CompanyId_Type",
                table: "TireInspectionDamages",
                columns: new[] { "CompanyId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_TireInspectionDamages_InspectionId_Type",
                table: "TireInspectionDamages",
                columns: new[] { "InspectionId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TireInspections_CompanyId_InspectedAt",
                table: "TireInspections",
                columns: new[] { "CompanyId", "InspectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TireInspections_CompanyId_TireId_InspectedAt",
                table: "TireInspections",
                columns: new[] { "CompanyId", "TireId", "InspectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TireInspections_CompanyId_VehicleId_InspectedAt",
                table: "TireInspections",
                columns: new[] { "CompanyId", "VehicleId", "InspectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TireInspections_ImplementId",
                table: "TireInspections",
                column: "ImplementId");

            migrationBuilder.CreateIndex(
                name: "IX_TireInspections_InstallationId",
                table: "TireInspections",
                column: "InstallationId");

            migrationBuilder.CreateIndex(
                name: "IX_TireInspections_TireId",
                table: "TireInspections",
                column: "TireId");

            migrationBuilder.CreateIndex(
                name: "IX_TireInspections_VehicleId",
                table: "TireInspections",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_TireInstallations_CompanyId_ImplementId_InstalledAt",
                table: "TireInstallations",
                columns: new[] { "CompanyId", "ImplementId", "InstalledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TireInstallations_CompanyId_InstalledAt",
                table: "TireInstallations",
                columns: new[] { "CompanyId", "InstalledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TireInstallations_CompanyId_TireId_InstalledAt",
                table: "TireInstallations",
                columns: new[] { "CompanyId", "TireId", "InstalledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TireInstallations_CompanyId_VehicleId_InstalledAt",
                table: "TireInstallations",
                columns: new[] { "CompanyId", "VehicleId", "InstalledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TireInstallations_OpenByImplementPosition",
                table: "TireInstallations",
                columns: new[] { "ImplementId", "PositionCode" },
                unique: true,
                filter: "[RemovedAt] IS NULL AND [ImplementId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TireInstallations_OpenByTire",
                table: "TireInstallations",
                column: "TireId",
                unique: true,
                filter: "[RemovedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TireInstallations_OpenByVehiclePosition",
                table: "TireInstallations",
                columns: new[] { "VehicleId", "PositionCode" },
                unique: true,
                filter: "[RemovedAt] IS NULL AND [VehicleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TireInstallations_RemovalRotationId",
                table: "TireInstallations",
                column: "RemovalRotationId");

            migrationBuilder.CreateIndex(
                name: "IX_TireInstallations_RotationId",
                table: "TireInstallations",
                column: "RotationId");

            migrationBuilder.CreateIndex(
                name: "IX_TireLayoutAxles_CompanyId",
                table: "TireLayoutAxles",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TireLayoutAxles_TireLayoutId_Number",
                table: "TireLayoutAxles",
                columns: new[] { "TireLayoutId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TireLayouts_CompanyId_Name",
                table: "TireLayouts",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TireModels_CompanyId_Brand_Name_Size",
                table: "TireModels",
                columns: new[] { "CompanyId", "Brand", "Name", "Size" },
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TireRotations_CompanyId_ImplementId_PerformedAt",
                table: "TireRotations",
                columns: new[] { "CompanyId", "ImplementId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TireRotations_CompanyId_VehicleId_PerformedAt",
                table: "TireRotations",
                columns: new[] { "CompanyId", "VehicleId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TireRotations_ImplementId",
                table: "TireRotations",
                column: "ImplementId");

            migrationBuilder.CreateIndex(
                name: "IX_TireRotations_VehicleId",
                table: "TireRotations",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Tires_CompanyId_Code",
                table: "Tires",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tires_CompanyId_Sequence",
                table: "Tires",
                columns: new[] { "CompanyId", "Sequence" },
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tires_CompanyId_Status",
                table: "Tires",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tires_CompanyId_TireModelId",
                table: "Tires",
                columns: new[] { "CompanyId", "TireModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tires_TireModelId",
                table: "Tires",
                column: "TireModelId");

            migrationBuilder.CreateIndex(
                name: "IX_TireServiceOrders_CompanyId_Status",
                table: "TireServiceOrders",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TireServiceOrders_CompanyId_TireId_SentAt",
                table: "TireServiceOrders",
                columns: new[] { "CompanyId", "TireId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TireServiceOrders_OpenByTire",
                table: "TireServiceOrders",
                column: "TireId",
                unique: true,
                filter: "[Status] = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_TireServiceOrders_WorkshopId",
                table: "TireServiceOrders",
                column: "WorkshopId");

            migrationBuilder.CreateIndex(
                name: "IX_TireSettings_CompanyId",
                table: "TireSettings",
                column: "CompanyId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Implements_TireLayouts_TireLayoutId",
                table: "Implements",
                column: "TireLayoutId",
                principalTable: "TireLayouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_TireLayouts_TireLayoutId",
                table: "Vehicles",
                column: "TireLayoutId",
                principalTable: "TireLayouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Implements_TireLayouts_TireLayoutId",
                table: "Implements");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_TireLayouts_TireLayoutId",
                table: "Vehicles");

            migrationBuilder.DropTable(
                name: "TireAnomalies");

            migrationBuilder.DropTable(
                name: "TireCosts");

            migrationBuilder.DropTable(
                name: "TireInspectionDamages");

            migrationBuilder.DropTable(
                name: "TireLayoutAxles");

            migrationBuilder.DropTable(
                name: "TireSettings");

            migrationBuilder.DropTable(
                name: "TireServiceOrders");

            migrationBuilder.DropTable(
                name: "TireInspections");

            migrationBuilder.DropTable(
                name: "TireLayouts");

            migrationBuilder.DropTable(
                name: "TireInstallations");

            migrationBuilder.DropTable(
                name: "TireRotations");

            migrationBuilder.DropTable(
                name: "Tires");

            migrationBuilder.DropTable(
                name: "TireModels");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_TireLayoutId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_OperationalEvents_CompanyId_TireId_OccurredAt",
                table: "OperationalEvents");

            migrationBuilder.DropIndex(
                name: "IX_Implements_TireLayoutId",
                table: "Implements");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 180, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 181, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 182, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 183, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 184, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 185, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 186, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 187, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 188, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 189, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 190, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 191, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 180, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 181, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 182, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 183, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 184, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 185, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 186, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 187, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 188, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 189, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 190, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 191, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 180, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 181, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 182, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 183, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 184, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 185, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 186, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 187, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 188, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 189, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 190, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 191, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 180, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 186, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 180, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 181, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 182, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 183, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 184, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 185, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 186, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 187, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 188, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 189, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 190, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 191, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 180, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 190, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 180, 8 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 180);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 181);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 182);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 183);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 184);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 185);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 186);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 187);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 188);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 189);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 190);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 191);

            migrationBuilder.DropColumn(
                name: "TireLayoutId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "TireId",
                table: "OperationalEvents");

            migrationBuilder.DropColumn(
                name: "TireLayoutId",
                table: "Implements");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "Gerencia a frota e a operação: cadastros, alocações, hodômetro, documentos, checklists, ocorrências, manutenção e combustível.");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "Description",
                value: "Operação diária: motoristas, alocações, hodômetro, documentos, checklists, ocorrências e registro de abastecimentos.");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "Description",
                value: "Atualiza a situação de veículos e implementos, acompanha ocorrências e executa a manutenção da frota.");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "Description",
                value: "Consulta a frota e os custos de manutenção e combustível.");
        }
    }
}
