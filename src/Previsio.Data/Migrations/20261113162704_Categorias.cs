using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class Categorias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CategoriaBlingId",
                table: "contas_pagar",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CategoriaConferidaEm",
                table: "contas_pagar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoriaPadrao",
                table: "clientes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "categorias_financeiras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    BlingId = table.Column<long>(type: "bigint", nullable: false),
                    BlingIdPai = table.Column<long>(type: "bigint", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    AtualizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorias_financeiras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_categorias_financeiras_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regras_categoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    CnpjFornecedor = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    Categoria = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regras_categoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regras_categoria_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_financeiras_EmpresaId_BlingId",
                table: "categorias_financeiras",
                columns: new[] { "EmpresaId", "BlingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regras_categoria_ClienteId_CnpjFornecedor",
                table: "regras_categoria",
                columns: new[] { "ClienteId", "CnpjFornecedor" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categorias_financeiras");

            migrationBuilder.DropTable(
                name: "regras_categoria");

            migrationBuilder.DropColumn(
                name: "CategoriaBlingId",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "CategoriaConferidaEm",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "CategoriaPadrao",
                table: "clientes");
        }
    }
}
