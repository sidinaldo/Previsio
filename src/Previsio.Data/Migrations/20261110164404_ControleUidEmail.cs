using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class ControleUidEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "UidEmail",
                table: "falhas_processamento",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImapUidValidity",
                table: "clientes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImapUltimoUid",
                table: "clientes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_falhas_processamento_ClienteId_UidEmail",
                table: "falhas_processamento",
                columns: new[] { "ClienteId", "UidEmail" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_falhas_processamento_ClienteId_UidEmail",
                table: "falhas_processamento");

            migrationBuilder.DropColumn(
                name: "UidEmail",
                table: "falhas_processamento");

            migrationBuilder.DropColumn(
                name: "ImapUidValidity",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "ImapUltimoUid",
                table: "clientes");
        }
    }
}
