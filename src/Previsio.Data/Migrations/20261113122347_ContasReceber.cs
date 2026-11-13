using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContasReceber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MostrarFluxoCaixa",
                table: "clientes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "contas_receber",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    BlingId = table.Column<long>(type: "bigint", nullable: false),
                    Vencimento = table.Column<DateTime>(type: "date", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Situacao = table.Column<int>(type: "integer", nullable: false),
                    BlingContatoId = table.Column<long>(type: "bigint", nullable: true),
                    ClienteNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SincronizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contas_receber", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contas_receber_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_EmpresaId_BlingId",
                table: "contas_receber",
                columns: new[] { "EmpresaId", "BlingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_Vencimento",
                table: "contas_receber",
                column: "Vencimento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contas_receber");

            migrationBuilder.DropColumn(
                name: "MostrarFluxoCaixa",
                table: "clientes");
        }
    }
}
