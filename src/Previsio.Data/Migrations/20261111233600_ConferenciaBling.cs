using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConferenciaBling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Redes já cadastradas: conferência ligada, R$ 0,10 e 0 dias de diferença, fechar pendentes encontradas
            migrationBuilder.AddColumn<bool>(
                name: "ConferenciaExigeNumeroNota",
                table: "clientes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ConferenciaFecharPendentes",
                table: "clientes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ConferenciaToleranciaDias",
                table: "clientes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ConferenciaToleranciaValor",
                table: "clientes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.10m);

            migrationBuilder.AddColumn<bool>(
                name: "ConferirBlingAntesDeLancar",
                table: "clientes",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConferenciaExigeNumeroNota",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "ConferenciaFecharPendentes",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "ConferenciaToleranciaDias",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "ConferenciaToleranciaValor",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "ConferirBlingAntesDeLancar",
                table: "clientes");
        }
    }
}
