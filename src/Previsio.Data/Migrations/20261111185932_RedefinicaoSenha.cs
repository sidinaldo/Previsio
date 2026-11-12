using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class RedefinicaoSenha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RedefinicaoSenhaExpiraEm",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedefinicaoSenhaHash",
                table: "usuarios",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_RedefinicaoSenhaHash",
                table: "usuarios",
                column: "RedefinicaoSenhaHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_RedefinicaoSenhaHash",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "RedefinicaoSenhaExpiraEm",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "RedefinicaoSenhaHash",
                table: "usuarios");
        }
    }
}
