using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class FalhasProcessamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "falhas_processamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OcorridaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    EmpresaId = table.Column<int>(type: "integer", nullable: true),
                    NotaFiscalId = table.Column<int>(type: "integer", nullable: true),
                    Etapa = table.Column<int>(type: "integer", nullable: false),
                    Mensagem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Metodo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Endereco = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StatusHttp = table.Column<int>(type: "integer", nullable: true),
                    Resposta = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NumeroNota = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ChaveAcesso = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    Fornecedor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CnpjFornecedor = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    ValorNota = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    NumeroParcela = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ValorParcela = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    VencimentoParcela = table.Column<DateTime>(type: "date", nullable: true),
                    Origem = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_falhas_processamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_falhas_processamento_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_falhas_processamento_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_falhas_processamento_notas_fiscais_NotaFiscalId",
                        column: x => x.NotaFiscalId,
                        principalTable: "notas_fiscais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_falhas_processamento_ClienteId_OcorridaEm",
                table: "falhas_processamento",
                columns: new[] { "ClienteId", "OcorridaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_falhas_processamento_EmpresaId",
                table: "falhas_processamento",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_falhas_processamento_NotaFiscalId",
                table: "falhas_processamento",
                column: "NotaFiscalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "falhas_processamento");
        }
    }
}
