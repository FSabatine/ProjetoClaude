using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace REC4.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEscritorioUsuarioVinculosAndPersonalizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LimiteDiasEdicaoFinanceiro",
                table: "Usuarios",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PessoaId",
                table: "Usuarios",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TemPermissaoPersonalizada",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TipoUsuario",
                table: "Usuarios",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Escritorios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Escritorios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioEmitentes",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioEmitentes", x => new { x.UsuarioId, x.PessoaId });
                    table.ForeignKey(
                        name: "FK_UsuarioEmitentes_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuarioEmitentes_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioPermissoes",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioPermissoes", x => new { x.UsuarioId, x.PermissaoId });
                    table.ForeignKey(
                        name: "FK_UsuarioPermissoes_Permissoes_PermissaoId",
                        column: x => x.PermissaoId,
                        principalTable: "Permissoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuarioPermissoes_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioEscritorios",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EscritorioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrincipal = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioEscritorios", x => new { x.UsuarioId, x.EscritorioId });
                    table.ForeignKey(
                        name: "FK_UsuarioEscritorios_Escritorios_EscritorioId",
                        column: x => x.EscritorioId,
                        principalTable: "Escritorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuarioEscritorios_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissoes",
                columns: new[] { "Id", "Chave", "Descricao" },
                values: new object[,]
                {
                    { 14, "Escritorio.Visualizar", "Visualizar escritórios cadastrados" },
                    { 15, "Escritorio.Criar", "Cadastrar novos escritórios" },
                    { 16, "Escritorio.Editar", "Editar escritórios existentes" }
                });

            migrationBuilder.InsertData(
                table: "GrupoPermissoes",
                columns: new[] { "GrupoId", "PermissaoId" },
                values: new object[,]
                {
                    { 1, 14 },
                    { 1, 15 },
                    { 1, 16 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_PessoaId",
                table: "Usuarios",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioEmitentes_PessoaId",
                table: "UsuarioEmitentes",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioEscritorios_EscritorioId",
                table: "UsuarioEscritorios",
                column: "EscritorioId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioEscritorios_UsuarioId_Principal",
                table: "UsuarioEscritorios",
                column: "UsuarioId",
                unique: true,
                filter: "[IsPrincipal] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioPermissoes_PermissaoId",
                table: "UsuarioPermissoes",
                column: "PermissaoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Pessoas_PessoaId",
                table: "Usuarios",
                column: "PessoaId",
                principalTable: "Pessoas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Pessoas_PessoaId",
                table: "Usuarios");

            migrationBuilder.DropTable(
                name: "UsuarioEmitentes");

            migrationBuilder.DropTable(
                name: "UsuarioEscritorios");

            migrationBuilder.DropTable(
                name: "UsuarioPermissoes");

            migrationBuilder.DropTable(
                name: "Escritorios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_PessoaId",
                table: "Usuarios");

            migrationBuilder.DeleteData(
                table: "GrupoPermissoes",
                keyColumns: new[] { "GrupoId", "PermissaoId" },
                keyValues: new object[] { 1, 14 });

            migrationBuilder.DeleteData(
                table: "GrupoPermissoes",
                keyColumns: new[] { "GrupoId", "PermissaoId" },
                keyValues: new object[] { 1, 15 });

            migrationBuilder.DeleteData(
                table: "GrupoPermissoes",
                keyColumns: new[] { "GrupoId", "PermissaoId" },
                keyValues: new object[] { 1, 16 });

            migrationBuilder.DeleteData(
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DropColumn(
                name: "LimiteDiasEdicaoFinanceiro",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PessoaId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TemPermissaoPersonalizada",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TipoUsuario",
                table: "Usuarios");
        }
    }
}
