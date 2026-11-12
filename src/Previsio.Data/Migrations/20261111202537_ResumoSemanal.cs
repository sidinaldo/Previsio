using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResumoSemanal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Redes já cadastradas recebem o resumo na segunda-feira às 7h (o mesmo padrão das novas)
            migrationBuilder.AddColumn<bool>(
                name: "ResumoSemanalAtivo",
                table: "clientes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ResumoSemanalDia",
                table: "clientes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResumoSemanalEnviadoEm",
                table: "clientes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResumoSemanalHora",
                table: "clientes",
                type: "integer",
                nullable: false,
                defaultValue: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResumoSemanalAtivo",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "ResumoSemanalDia",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "ResumoSemanalEnviadoEm",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "ResumoSemanalHora",
                table: "clientes");
        }
    }
}
