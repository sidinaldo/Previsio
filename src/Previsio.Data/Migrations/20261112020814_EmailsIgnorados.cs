using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailsIgnorados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "emails_ignorados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    EmpresaCaixaId = table.Column<int>(type: "integer", nullable: true),
                    Uid = table.Column<long>(type: "bigint", nullable: false),
                    MensagemId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Remetente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NomeRemetente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Assunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Corpo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EnviadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Anexos = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PdfNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Pdf = table.Column<byte[]>(type: "bytea", nullable: true),
                    TratadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TratadoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResultadoTratamento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emails_ignorados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_emails_ignorados_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_emails_ignorados_empresas_EmpresaCaixaId",
                        column: x => x.EmpresaCaixaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_emails_ignorados_ClienteId_LidoEm",
                table: "emails_ignorados",
                columns: new[] { "ClienteId", "LidoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_emails_ignorados_ClienteId_MensagemId",
                table: "emails_ignorados",
                columns: new[] { "ClienteId", "MensagemId" });

            migrationBuilder.CreateIndex(
                name: "IX_emails_ignorados_EmpresaCaixaId",
                table: "emails_ignorados",
                column: "EmpresaCaixaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "emails_ignorados");
        }
    }
}
