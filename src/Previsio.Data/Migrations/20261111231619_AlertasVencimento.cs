using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlertasVencimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Redes já cadastradas: aviso de vencidas no dia seguinte e alerta diário às 7h
            migrationBuilder.AddColumn<bool>(
                name: "AlertaVencimentosAtivo",
                table: "clientes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AlertaVencimentosEnviadoEm",
                table: "clientes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AlertaVencimentosHora",
                table: "clientes",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "AvisoVencidasDias",
                table: "clientes",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertaVencimentosAtivo",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "AlertaVencimentosEnviadoEm",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "AlertaVencimentosHora",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "AvisoVencidasDias",
                table: "clientes");
        }
    }
}
