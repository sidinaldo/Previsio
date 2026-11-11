using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotasSoPdfECertificado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CertificadoPfxProtegido",
                table: "empresas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificadoSenhaProtegida",
                table: "empresas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificadoTitular",
                table: "empresas",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CertificadoValidoAte",
                table: "empresas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uf",
                table: "empresas",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notas_so_pdf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    EmpresaCaixaId = table.Column<int>(type: "integer", nullable: true),
                    MensagemId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Remetente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NomeRemetente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Assunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OrigemEmail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RecebidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PdfNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Pdf = table.Column<byte[]>(type: "bytea", nullable: false),
                    ChaveAcesso = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    OrigemChave = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Situacao = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TentativasSefaz = table.Column<int>(type: "integer", nullable: false),
                    ProximaTentativaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CienciaEnviadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmpresaDestinoId = table.Column<int>(type: "integer", nullable: true),
                    XmlPedidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotaFiscalId = table.Column<int>(type: "integer", nullable: true),
                    ResolvidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvidaPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notas_so_pdf", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notas_so_pdf_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notas_so_pdf_empresas_EmpresaCaixaId",
                        column: x => x.EmpresaCaixaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notas_so_pdf_ClienteId_MensagemId",
                table: "notas_so_pdf",
                columns: new[] { "ClienteId", "MensagemId" });

            migrationBuilder.CreateIndex(
                name: "IX_notas_so_pdf_ClienteId_Situacao",
                table: "notas_so_pdf",
                columns: new[] { "ClienteId", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "IX_notas_so_pdf_EmpresaCaixaId",
                table: "notas_so_pdf",
                column: "EmpresaCaixaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notas_so_pdf");

            migrationBuilder.DropColumn(
                name: "CertificadoPfxProtegido",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "CertificadoSenhaProtegida",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "CertificadoTitular",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "CertificadoValidoAte",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "Uf",
                table: "empresas");
        }
    }
}
