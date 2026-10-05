using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntelligenceAndAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Trigger = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Threshold = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    LookbackDays = table.Column<int>(type: "int", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreateAlert = table.Column<bool>(type: "bit", nullable: false),
                    NotifyMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NotifyUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationRules_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationRules_Users_NotifyUserId",
                        column: x => x.NotifyUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutomationExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutomationRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Evaluated = table.Column<int>(type: "int", nullable: false),
                    AlertsCreated = table.Column<int>(type: "int", nullable: false),
                    AlertsUpdated = table.Column<int>(type: "int", nullable: false),
                    AlertsResolved = table.Column<int>(type: "int", nullable: false),
                    NotificationsCreated = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationExecutions_AutomationRules_AutomationRuleId",
                        column: x => x.AutomationRuleId,
                        principalTable: "AutomationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationExecutions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutomationRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Trigger = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecommendedAction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tab = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DedupKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastDetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DetectionCount = table.Column<int>(type: "int", nullable: false),
                    RecurrenceCount = table.Column<int>(type: "int", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReadBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosingNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AutoResolved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetAlerts_AutomationRules_AutomationRuleId",
                        column: x => x.AutomationRuleId,
                        principalTable: "AutomationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetAlerts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetAlertId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Link = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserNotifications_FleetAlerts_FleetAlertId",
                        column: x => x.FleetAlertId,
                        principalTable: "FleetAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Module" },
                values: new object[,]
                {
                    { 210, "Visualizar alertas, \"Requer atenção\" e notificações", "alerts.view", "alerts" },
                    { 211, "Assumir, resolver e descartar alertas", "alerts.manage", "alerts" },
                    { 212, "Configurar regras de automação", "automation.manage", "automation" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 210, 1 },
                    { 211, 1 },
                    { 212, 1 },
                    { 210, 2 },
                    { 211, 2 },
                    { 212, 2 },
                    { 210, 3 },
                    { 211, 3 },
                    { 212, 3 },
                    { 210, 4 },
                    { 211, 4 },
                    { 210, 5 },
                    { 211, 5 },
                    { 210, 6 },
                    { 211, 6 },
                    { 210, 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_AutomationRuleId",
                table: "AutomationExecutions",
                column: "AutomationRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_CompanyId_AutomationRuleId_StartedAt",
                table: "AutomationExecutions",
                columns: new[] { "CompanyId", "AutomationRuleId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_CompanyId_FinishedAt",
                table: "AutomationExecutions",
                columns: new[] { "CompanyId", "FinishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_CompanyId_IsActive",
                table: "AutomationRules",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_NotifyUserId",
                table: "AutomationRules",
                column: "NotifyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetAlerts_AutomationRuleId",
                table: "FleetAlerts",
                column: "AutomationRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetAlerts_CompanyId_AutomationRuleId_DedupKey_Status",
                table: "FleetAlerts",
                columns: new[] { "CompanyId", "AutomationRuleId", "DedupKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetAlerts_CompanyId_Status_Priority",
                table: "FleetAlerts",
                columns: new[] { "CompanyId", "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetAlerts_CompanyId_VehicleId_Status",
                table: "FleetAlerts",
                columns: new[] { "CompanyId", "VehicleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_FleetAlerts_OpenFinding",
                table: "FleetAlerts",
                columns: new[] { "CompanyId", "AutomationRuleId", "DedupKey" },
                unique: true,
                filter: "[Status] IN ('New', 'Read', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_CompanyId_UserId_ReadAt_CreatedAt",
                table: "UserNotifications",
                columns: new[] { "CompanyId", "UserId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_FleetAlertId",
                table: "UserNotifications",
                column: "FleetAlertId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId",
                table: "UserNotifications",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationExecutions");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "FleetAlerts");

            migrationBuilder.DropTable(
                name: "AutomationRules");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 210, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 211, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 212, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 210, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 211, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 212, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 210, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 211, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 212, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 210, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 211, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 210, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 211, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 210, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 211, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 210, 8 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 210);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 211);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 212);
        }
    }
}
