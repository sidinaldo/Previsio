using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class FormasPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TipoPagamento",
                table: "notas_fiscais",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FormaPagamentoBlingId",
                table: "contas_pagar",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormaPagamentoPadrao",
                table: "clientes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "formas_pagamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    BlingId = table.Column<long>(type: "bigint", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TipoPagamento = table.Column<int>(type: "integer", nullable: false),
                    Situacao = table.Column<int>(type: "integer", nullable: false),
                    Grupo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AtualizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_formas_pagamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_formas_pagamento_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regras_forma_pagamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    CnpjFornecedor = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    Forma = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regras_forma_pagamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regras_forma_pagamento_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_formas_pagamento_EmpresaId_BlingId",
                table: "formas_pagamento",
                columns: new[] { "EmpresaId", "BlingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regras_forma_pagamento_ClienteId_CnpjFornecedor",
                table: "regras_forma_pagamento",
                columns: new[] { "ClienteId", "CnpjFornecedor" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "formas_pagamento");

            migrationBuilder.DropTable(
                name: "regras_forma_pagamento");

            migrationBuilder.DropColumn(
                name: "TipoPagamento",
                table: "notas_fiscais");

            migrationBuilder.DropColumn(
                name: "FormaPagamentoBlingId",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "FormaPagamentoPadrao",
                table: "clientes");
        }
    }
}
