using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class BuscaSefaz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SefazBuscaAtiva",
                table: "empresas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SefazProximaBuscaEm",
                table: "empresas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SefazSituacao",
                table: "empresas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SefazUltimaBuscaEm",
                table: "empresas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SefazUltimoNsu",
                table: "empresas",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SefazBuscaAtiva",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "SefazProximaBuscaEm",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "SefazSituacao",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "SefazUltimaBuscaEm",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "SefazUltimoNsu",
                table: "empresas");
        }
    }
}
