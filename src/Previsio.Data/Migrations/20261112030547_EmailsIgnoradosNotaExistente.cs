using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailsIgnoradosNotaExistente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NotaFiscalId",
                table: "emails_ignorados",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroNota",
                table: "emails_ignorados",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotaFiscalId",
                table: "emails_ignorados");

            migrationBuilder.DropColumn(
                name: "NumeroNota",
                table: "emails_ignorados");
        }
    }
}
