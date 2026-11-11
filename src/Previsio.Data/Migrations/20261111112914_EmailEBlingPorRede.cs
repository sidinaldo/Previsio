using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailEBlingPorRede : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImapHost",
                table: "empresas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ImapPorta",
                table: "empresas",
                type: "integer",
                nullable: false,
                defaultValue: 993);

            migrationBuilder.AddColumn<string>(
                name: "ImapSenhaProtegida",
                table: "empresas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ImapSsl",
                table: "empresas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "ImapUidValidity",
                table: "empresas",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImapUltimoUid",
                table: "empresas",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImapUsuario",
                table: "empresas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BlingClientId",
                table: "clientes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlingClientSecretProtegido",
                table: "clientes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImapHost",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "ImapPorta",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "ImapSenhaProtegida",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "ImapSsl",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "ImapUidValidity",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "ImapUltimoUid",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "ImapUsuario",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "BlingClientId",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "BlingClientSecretProtegido",
                table: "clientes");
        }
    }
}
