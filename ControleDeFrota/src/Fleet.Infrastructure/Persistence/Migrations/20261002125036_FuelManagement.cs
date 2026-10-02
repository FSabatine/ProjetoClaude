using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FuelManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedConsumption",
                table: "Vehicles",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FuelTankCapacity",
                table: "Vehicles",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SecondaryFuelTankCapacity",
                table: "Vehicles",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FuelingId",
                table: "OdometerReadings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FuelSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TankTolerancePercent = table.Column<int>(type: "int", nullable: false),
                    PriceDeviationPercent = table.Column<int>(type: "int", nullable: false),
                    ConsumptionDeviationPercent = table.Column<int>(type: "int", nullable: false),
                    MinHoursBetweenFuelings = table.Column<int>(type: "int", nullable: false),
                    RequireDriver = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuelSettings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FuelStations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cnpj = table.Column<string>(type: "varchar(14)", unicode: false, maxLength: 14, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Complement = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Neighborhood = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    State = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: true),
                    ZipCode = table.Column<string>(type: "char(8)", unicode: false, fixedLength: true, maxLength: 8, nullable: true),
                    Phone = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuelStations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FuelTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuelTypes_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Fuelings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FuelStationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FuelTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FueledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FueledOn = table.Column<DateOnly>(type: "date", nullable: false),
                    OdometerKm = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    IsFullTank = table.Column<bool>(type: "bit", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConsumptionResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SegmentDistanceKm = table.Column<int>(type: "int", nullable: true),
                    SegmentQuantity = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: true),
                    SegmentCost = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    Consumption = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ExpectedConsumption = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    SegmentExpectedQuantity = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: true),
                    BaselineSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ConsumptionDeviationPercent = table.Column<decimal>(type: "decimal(8,1)", precision: 8, scale: 1, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fuelings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fuelings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fuelings_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fuelings_FuelStations_FuelStationId",
                        column: x => x.FuelStationId,
                        principalTable: "FuelStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fuelings_FuelTypes_FuelTypeId",
                        column: x => x.FuelTypeId,
                        principalTable: "FuelTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fuelings_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FuelPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuelStationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuelTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuelPrices_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FuelPrices_FuelStations_FuelStationId",
                        column: x => x.FuelStationId,
                        principalTable: "FuelStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FuelPrices_FuelTypes_FuelTypeId",
                        column: x => x.FuelTypeId,
                        principalTable: "FuelTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FuelingAnomalies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuelingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ExpectedValue = table.Column<decimal>(type: "decimal(14,4)", precision: 14, scale: 4, nullable: true),
                    ActualValue = table.Column<decimal>(type: "decimal(14,4)", precision: 14, scale: 4, nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelingAnomalies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuelingAnomalies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FuelingAnomalies_Fuelings_FuelingId",
                        column: x => x.FuelingId,
                        principalTable: "Fuelings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FuelingCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuelingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrectedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Changes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelingCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuelingCorrections_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FuelingCorrections_Fuelings_FuelingId",
                        column: x => x.FuelingId,
                        principalTable: "Fuelings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Module" },
                values: new object[,]
                {
                    { 170, "Visualizar abastecimentos, consumo e relatórios de combustível", "fuel.view", "fuel" },
                    { 171, "Registrar abastecimentos", "fuel.create", "fuel" },
                    { 172, "Corrigir abastecimentos", "fuel.correct", "fuel" },
                    { 173, "Cancelar abastecimentos", "fuel.cancel", "fuel" },
                    { 174, "Revisar abastecimentos com alerta", "fuel.reviewanomalies", "fuel" },
                    { 175, "Cadastrar postos e preços de referência", "fuel.managestations", "fuel" },
                    { 176, "Configurar tipos de combustível e limites de alerta", "fuel.configure", "fuel" },
                    { 177, "Visualizar custos de combustível", "fuel.viewcosts", "fuel" }
                });

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
                keyValue: 6,
                column: "Description",
                value: "Consulta a frota e os custos de manutenção e combustível.");

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 170, 1 },
                    { 171, 1 },
                    { 172, 1 },
                    { 173, 1 },
                    { 174, 1 },
                    { 175, 1 },
                    { 176, 1 },
                    { 177, 1 },
                    { 170, 2 },
                    { 171, 2 },
                    { 172, 2 },
                    { 173, 2 },
                    { 174, 2 },
                    { 175, 2 },
                    { 176, 2 },
                    { 177, 2 },
                    { 170, 3 },
                    { 171, 3 },
                    { 172, 3 },
                    { 173, 3 },
                    { 174, 3 },
                    { 175, 3 },
                    { 176, 3 },
                    { 177, 3 },
                    { 170, 4 },
                    { 171, 4 },
                    { 170, 5 },
                    { 170, 6 },
                    { 177, 6 },
                    { 170, 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_OdometerReadings_FuelingId",
                table: "OdometerReadings",
                column: "FuelingId");

            migrationBuilder.CreateIndex(
                name: "IX_FuelingAnomalies_CompanyId_Type",
                table: "FuelingAnomalies",
                columns: new[] { "CompanyId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_FuelingAnomalies_FuelingId",
                table: "FuelingAnomalies",
                column: "FuelingId");

            migrationBuilder.CreateIndex(
                name: "IX_FuelingCorrections_CompanyId",
                table: "FuelingCorrections",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_FuelingCorrections_FuelingId",
                table: "FuelingCorrections",
                column: "FuelingId");

            migrationBuilder.CreateIndex(
                name: "IX_Fuelings_CompanyId_DriverId_FueledOn",
                table: "Fuelings",
                columns: new[] { "CompanyId", "DriverId", "FueledOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Fuelings_CompanyId_FueledOn",
                table: "Fuelings",
                columns: new[] { "CompanyId", "FueledOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Fuelings_CompanyId_FuelStationId_FueledOn",
                table: "Fuelings",
                columns: new[] { "CompanyId", "FuelStationId", "FueledOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Fuelings_CompanyId_FuelTypeId_FueledOn",
                table: "Fuelings",
                columns: new[] { "CompanyId", "FuelTypeId", "FueledOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Fuelings_CompanyId_Status",
                table: "Fuelings",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Fuelings_CompanyId_VehicleId_FueledAt",
                table: "Fuelings",
                columns: new[] { "CompanyId", "VehicleId", "FueledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Fuelings_DriverId",
                table: "Fuelings",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Fuelings_FuelStationId",
                table: "Fuelings",
                column: "FuelStationId");

            migrationBuilder.CreateIndex(
                name: "IX_Fuelings_FuelTypeId",
                table: "Fuelings",
                column: "FuelTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Fuelings_VehicleId",
                table: "Fuelings",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_FuelPrices_CompanyId_FuelStationId_FuelTypeId_EffectiveFrom",
                table: "FuelPrices",
                columns: new[] { "CompanyId", "FuelStationId", "FuelTypeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_FuelPrices_FuelStationId",
                table: "FuelPrices",
                column: "FuelStationId");

            migrationBuilder.CreateIndex(
                name: "IX_FuelPrices_FuelTypeId",
                table: "FuelPrices",
                column: "FuelTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FuelSettings_CompanyId",
                table: "FuelSettings",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FuelStations_CompanyId_Cnpj",
                table: "FuelStations",
                columns: new[] { "CompanyId", "Cnpj" },
                unique: true,
                filter: "[DeletedAt] IS NULL AND [Cnpj] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FuelStations_CompanyId_Name",
                table: "FuelStations",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_FuelTypes_CompanyId_Code",
                table: "FuelTypes",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FuelTypes_CompanyId_Name",
                table: "FuelTypes",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_OdometerReadings_Fuelings_FuelingId",
                table: "OdometerReadings",
                column: "FuelingId",
                principalTable: "Fuelings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OdometerReadings_Fuelings_FuelingId",
                table: "OdometerReadings");

            migrationBuilder.DropTable(
                name: "FuelingAnomalies");

            migrationBuilder.DropTable(
                name: "FuelingCorrections");

            migrationBuilder.DropTable(
                name: "FuelPrices");

            migrationBuilder.DropTable(
                name: "FuelSettings");

            migrationBuilder.DropTable(
                name: "Fuelings");

            migrationBuilder.DropTable(
                name: "FuelStations");

            migrationBuilder.DropTable(
                name: "FuelTypes");

            migrationBuilder.DropIndex(
                name: "IX_OdometerReadings_FuelingId",
                table: "OdometerReadings");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 170, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 171, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 172, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 173, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 174, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 175, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 176, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 177, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 170, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 171, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 172, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 173, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 174, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 175, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 176, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 177, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 170, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 171, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 172, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 173, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 174, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 175, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 176, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 177, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 170, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 171, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 170, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 170, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 177, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 170, 8 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 170);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 171);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 172);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 173);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 174);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 175);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 176);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 177);

            migrationBuilder.DropColumn(
                name: "ExpectedConsumption",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FuelTankCapacity",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "SecondaryFuelTankCapacity",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FuelingId",
                table: "OdometerReadings");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "Gerencia a frota e a operação: cadastros, alocações, hodômetro, documentos, checklists, ocorrências e manutenção.");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "Description",
                value: "Operação diária: motoristas, alocações, hodômetro, documentos, checklists e ocorrências.");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "Description",
                value: "Consulta a frota para fins financeiros.");
        }
    }
}
