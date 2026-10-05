using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrackingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackingProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TrackingProviders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackingProviders_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrackingDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ApiKeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApiKeyPrefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    KeyIssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackingDevices_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrackingDevices_TrackingProviders_TrackingProviderId",
                        column: x => x.TrackingProviderId,
                        principalTable: "TrackingProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleDevices_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleDevices_TrackingDevices_TrackingDeviceId",
                        column: x => x.TrackingDeviceId,
                        principalTable: "TrackingDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleDevices_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehiclePositions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    SpeedKmh = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Heading = table.Column<int>(type: "int", nullable: true),
                    Ignition = table.Column<bool>(type: "bit", nullable: true),
                    DeviceOdometerKm = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehiclePositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehiclePositions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehiclePositions_TrackingDevices_TrackingDeviceId",
                        column: x => x.TrackingDeviceId,
                        principalTable: "TrackingDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehiclePositions_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Module" },
                values: new object[,]
                {
                    { 214, "Visualizar o mapa e a localização dos veículos", "tracking.view", "tracking" },
                    { 215, "Configurar rastreadores e integrações", "tracking.manage", "tracking" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 214, 1 },
                    { 215, 1 },
                    { 214, 2 },
                    { 215, 2 },
                    { 214, 3 },
                    { 215, 3 },
                    { 214, 4 },
                    { 214, 5 },
                    { 214, 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackingDevices_ApiKeyHash",
                table: "TrackingDevices",
                column: "ApiKeyHash",
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingDevices_CompanyId_Identifier",
                table: "TrackingDevices",
                columns: new[] { "CompanyId", "Identifier" },
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingDevices_TrackingProviderId",
                table: "TrackingDevices",
                column: "TrackingProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingProviders_CompanyId_Name",
                table: "TrackingProviders",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleDevices_CompanyId_VehicleId_StartedAt",
                table: "VehicleDevices",
                columns: new[] { "CompanyId", "VehicleId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_VehicleDevices_ActiveDevice",
                table: "VehicleDevices",
                column: "TrackingDeviceId",
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_VehicleDevices_ActiveVehicle",
                table: "VehicleDevices",
                column: "VehicleId",
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePositions_CompanyId_RecordedAt",
                table: "VehiclePositions",
                columns: new[] { "CompanyId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePositions_CompanyId_VehicleId_RecordedAt",
                table: "VehiclePositions",
                columns: new[] { "CompanyId", "VehicleId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePositions_TrackingDeviceId_RecordedAt",
                table: "VehiclePositions",
                columns: new[] { "TrackingDeviceId", "RecordedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePositions_VehicleId",
                table: "VehiclePositions",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleDevices");

            migrationBuilder.DropTable(
                name: "VehiclePositions");

            migrationBuilder.DropTable(
                name: "TrackingDevices");

            migrationBuilder.DropTable(
                name: "TrackingProviders");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 214, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 215, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 214, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 215, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 214, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 215, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 214, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 214, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 214, 8 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 214);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 215);
        }
    }
}
