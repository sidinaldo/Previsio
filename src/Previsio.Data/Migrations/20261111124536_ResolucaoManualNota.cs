using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResolucaoManualNota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ObservacaoResolucao",
                table: "notas_fiscais",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvidaManualmenteEm",
                table: "notas_fiscais",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvidaManualmentePor",
                table: "notas_fiscais",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolvidaManualmentePorId",
                table: "notas_fiscais",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObservacaoResolucao",
                table: "notas_fiscais");

            migrationBuilder.DropColumn(
                name: "ResolvidaManualmenteEm",
                table: "notas_fiscais");

            migrationBuilder.DropColumn(
                name: "ResolvidaManualmentePor",
                table: "notas_fiscais");

            migrationBuilder.DropColumn(
                name: "ResolvidaManualmentePorId",
                table: "notas_fiscais");
        }
    }
}
