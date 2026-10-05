using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrackingLastPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackingDeviceLastPositions",
                columns: table => new
                {
                    TrackingDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    SpeedKmh = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Heading = table.Column<int>(type: "int", nullable: true),
                    Ignition = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingDeviceLastPositions", x => x.TrackingDeviceId);
                    table.ForeignKey(
                        name: "FK_TrackingDeviceLastPositions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrackingDeviceLastPositions_TrackingDevices_TrackingDeviceId",
                        column: x => x.TrackingDeviceId,
                        principalTable: "TrackingDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackingDeviceLastPositions_CompanyId_RecordedAt",
                table: "TrackingDeviceLastPositions",
                columns: new[] { "CompanyId", "RecordedAt" });

            // Backfill from the position history so databases that already have positions keep showing them on the map.
            // (TrackingDeviceId, RecordedAt) is unique in VehiclePositions, so this yields one row per device.
            migrationBuilder.Sql("""
                INSERT INTO TrackingDeviceLastPositions (TrackingDeviceId, CompanyId, VehicleId, RecordedAt, Latitude, Longitude, SpeedKmh, Heading, Ignition)
                SELECT p.TrackingDeviceId, p.CompanyId, p.VehicleId, p.RecordedAt, p.Latitude, p.Longitude, p.SpeedKmh, p.Heading, p.Ignition
                FROM VehiclePositions p
                INNER JOIN (SELECT TrackingDeviceId, MAX(RecordedAt) AS MaxAt FROM VehiclePositions GROUP BY TrackingDeviceId) m
                    ON m.TrackingDeviceId = p.TrackingDeviceId AND m.MaxAt = p.RecordedAt;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackingDeviceLastPositions");
        }
    }
}
