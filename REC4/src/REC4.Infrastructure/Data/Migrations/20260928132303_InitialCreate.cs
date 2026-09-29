using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace REC4.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Perfis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Perfis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pessoas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoPessoa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CpfCnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataNascimento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RG = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrgaoEmissorRG = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UFRG = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataEmissaoRG = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NomeMae = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NomePai = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhatsApp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pessoas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContasBancarias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Banco = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Agencia = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Conta = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DigitoConta = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    TipoConta = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsPrincipal = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasBancarias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContasBancarias_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Enderecos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Logradouro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Complemento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Bairro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UF = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CEP = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Pais = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IsPrincipal = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enderecos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enderecos_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PessoaPerfis",
                columns: table => new
                {
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerfilId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaPerfis", x => new { x.PessoaId, x.PerfilId });
                    table.ForeignKey(
                        name: "FK_PessoaPerfis_Perfis_PerfilId",
                        column: x => x.PerfilId,
                        principalTable: "Perfis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PessoaPerfis_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Perfis",
                columns: new[] { "Id", "Nome" },
                values: new object[,]
                {
                    { 1, "Cliente" },
                    { 2, "Fornecedor" },
                    { 3, "Parceiro" },
                    { 4, "Colaborador" },
                    { 5, "Emitente" },
                    { 6, "Seguradora" },
                    { 7, "Proprietário" },
                    { 8, "Corretora" },
                    { 9, "Reguladora" },
                    { 10, "Responsável Financeiro" },
                    { 11, "Agenciador" },
                    { 12, "Motorista" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContasBancarias_PessoaId_Principal",
                table: "ContasBancarias",
                column: "PessoaId",
                unique: true,
                filter: "[IsPrincipal] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Enderecos_PessoaId_Principal",
                table: "Enderecos",
                column: "PessoaId",
                unique: true,
                filter: "[IsPrincipal] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Perfis_Nome",
                table: "Perfis",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaPerfis_PerfilId",
                table: "PessoaPerfis",
                column: "PerfilId");

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_CpfCnpj_Ativas",
                table: "Pessoas",
                column: "CpfCnpj",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContasBancarias");

            migrationBuilder.DropTable(
                name: "Enderecos");

            migrationBuilder.DropTable(
                name: "PessoaPerfis");

            migrationBuilder.DropTable(
                name: "Perfis");

            migrationBuilder.DropTable(
                name: "Pessoas");
        }
    }
}
