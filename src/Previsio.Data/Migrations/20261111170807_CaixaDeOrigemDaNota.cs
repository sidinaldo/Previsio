using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaixaDeOrigemDaNota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmpresaCaixaId",
                table: "notas_fiscais",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notas_fiscais_EmpresaCaixaId",
                table: "notas_fiscais",
                column: "EmpresaCaixaId");

            migrationBuilder.AddForeignKey(
                name: "FK_notas_fiscais_empresas_EmpresaCaixaId",
                table: "notas_fiscais",
                column: "EmpresaCaixaId",
                principalTable: "empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notas_fiscais_empresas_EmpresaCaixaId",
                table: "notas_fiscais");

            migrationBuilder.DropIndex(
                name: "IX_notas_fiscais_EmpresaCaixaId",
                table: "notas_fiscais");

            migrationBuilder.DropColumn(
                name: "EmpresaCaixaId",
                table: "notas_fiscais");
        }
    }
}
