using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace REC4.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthUsuarioGrupoPermissao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Grupos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grupos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Chave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Login = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SenhaHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    GrupoId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GrupoPermissoes",
                columns: table => new
                {
                    GrupoId = table.Column<int>(type: "int", nullable: false),
                    PermissaoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrupoPermissoes", x => new { x.GrupoId, x.PermissaoId });
                    table.ForeignKey(
                        name: "FK_GrupoPermissoes_Grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GrupoPermissoes_Permissoes_PermissaoId",
                        column: x => x.PermissaoId,
                        principalTable: "Permissoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Grupos",
                columns: new[] { "Id", "Nome" },
                values: new object[,]
                {
                    { 1, "Controladoria" },
                    { 2, "Financeiro" },
                    { 3, "Logística" }
                });

            migrationBuilder.InsertData(
                table: "Permissoes",
                columns: new[] { "Id", "Chave", "Descricao" },
                values: new object[,]
                {
                    { 1, "Pessoa.Visualizar", "Visualizar pessoas cadastradas" },
                    { 2, "Pessoa.Criar", "Cadastrar novas pessoas" },
                    { 3, "Pessoa.Editar", "Editar ou inativar pessoas cadastradas" },
                    { 4, "Usuario.Visualizar", "Visualizar usuários do sistema" },
                    { 5, "Usuario.Criar", "Cadastrar novos usuários" },
                    { 6, "Usuario.Editar", "Editar usuários existentes" },
                    { 7, "Financeiro.Visualizar", "Visualizar lançamentos financeiros" },
                    { 8, "Financeiro.Criar", "Criar lançamentos financeiros" },
                    { 9, "Financeiro.Editar", "Editar lançamentos financeiros" },
                    { 10, "Conciliacao.Visualizar", "Visualizar conciliação financeira" },
                    { 11, "Conciliacao.Editar", "Editar conciliação financeira" },
                    { 12, "Documento.Emitir", "Emitir documentos de transporte" },
                    { 13, "Documento.Visualizar", "Visualizar documentos de transporte" }
                });

            migrationBuilder.InsertData(
                table: "GrupoPermissoes",
                columns: new[] { "GrupoId", "PermissaoId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 1, 2 },
                    { 1, 3 },
                    { 1, 4 },
                    { 1, 5 },
                    { 1, 6 },
                    { 1, 7 },
                    { 1, 8 },
                    { 1, 9 },
                    { 1, 10 },
                    { 1, 11 },
                    { 1, 12 },
                    { 1, 13 },
                    { 2, 1 },
                    { 2, 7 },
                    { 2, 8 },
                    { 2, 9 },
                    { 2, 10 },
                    { 2, 11 },
                    { 3, 1 },
                    { 3, 12 },
                    { 3, 13 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GrupoPermissoes_PermissaoId",
                table: "GrupoPermissoes",
                column: "PermissaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Grupos_Nome",
                table: "Grupos",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissoes_Chave",
                table: "Permissoes",
                column: "Chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_GrupoId",
                table: "Usuarios",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Login",
                table: "Usuarios",
                column: "Login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrupoPermissoes");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Permissoes");

            migrationBuilder.DropTable(
                name: "Grupos");
        }
    }
}
