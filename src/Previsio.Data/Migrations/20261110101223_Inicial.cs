using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImapHost = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImapPorta = table.Column<int>(type: "integer", nullable: false),
                    ImapSsl = table.Column<bool>(type: "boolean", nullable: false),
                    ImapUsuario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImapSenhaProtegida = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "empresas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    BlingAccessTokenProtegido = table.Column<string>(type: "text", nullable: true),
                    BlingRefreshTokenProtegido = table.Column<string>(type: "text", nullable: true),
                    BlingTokenExpiraEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimaSincronizacaoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empresas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_empresas_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: true),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Perfil = table.Column<int>(type: "integer", nullable: false),
                    CarimboSeguranca = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UltimoAcessoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usuarios_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "contas_pagar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    BlingId = table.Column<long>(type: "bigint", nullable: false),
                    Vencimento = table.Column<DateTime>(type: "date", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Situacao = table.Column<int>(type: "integer", nullable: false),
                    BlingContatoId = table.Column<long>(type: "bigint", nullable: true),
                    SincronizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contas_pagar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contas_pagar_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notas_fiscais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    EmpresaId = table.Column<int>(type: "integer", nullable: true),
                    ChaveAcesso = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Serie = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    DataEmissao = table.Column<DateTime>(type: "date", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CnpjDestinatario = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    CnpjFornecedor = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    NomeFornecedor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MensagemErro = table.Column<string>(type: "text", nullable: true),
                    Tentativas = table.Column<int>(type: "integer", nullable: false),
                    OrigemEmail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RecebidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LancadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notas_fiscais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notas_fiscais_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "parcelas_nota",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotaFiscalId = table.Column<int>(type: "integer", nullable: false),
                    NumeroParcela = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Vencimento = table.Column<DateTime>(type: "date", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    BlingContaPagarId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcelas_nota", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parcelas_nota_notas_fiscais_NotaFiscalId",
                        column: x => x.NotaFiscalId,
                        principalTable: "notas_fiscais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_EmpresaId_BlingId",
                table: "contas_pagar",
                columns: new[] { "EmpresaId", "BlingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_Vencimento",
                table: "contas_pagar",
                column: "Vencimento");

            migrationBuilder.CreateIndex(
                name: "IX_empresas_ClienteId_Cnpj",
                table: "empresas",
                columns: new[] { "ClienteId", "Cnpj" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notas_fiscais_ClienteId_ChaveAcesso",
                table: "notas_fiscais",
                columns: new[] { "ClienteId", "ChaveAcesso" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notas_fiscais_EmpresaId",
                table: "notas_fiscais",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_notas_fiscais_Status",
                table: "notas_fiscais",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_parcelas_nota_NotaFiscalId",
                table: "parcelas_nota",
                column: "NotaFiscalId");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_ClienteId",
                table: "usuarios",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contas_pagar");

            migrationBuilder.DropTable(
                name: "parcelas_nota");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "notas_fiscais");

            migrationBuilder.DropTable(
                name: "empresas");

            migrationBuilder.DropTable(
                name: "clientes");
        }
    }
}
