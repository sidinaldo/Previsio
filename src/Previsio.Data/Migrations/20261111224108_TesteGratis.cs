using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class TesteGratis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ContratadoEm",
                table: "clientes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Contrato",
                table: "clientes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TesteAvisoEncerradoEnviadoEm",
                table: "clientes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TesteAvisoFinalEnviadoEm",
                table: "clientes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TesteTerminaEm",
                table: "clientes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContratadoEm",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "Contrato",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "TesteAvisoEncerradoEnviadoEm",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "TesteAvisoFinalEnviadoEm",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "TesteTerminaEm",
                table: "clientes");
        }
    }
}
