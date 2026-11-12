using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <summary>
    /// Perfil Gestor (2): só dados, a coluna "Perfil" já é inteira.
    /// Em cada rede, o usuário ativo mais antigo vira gestor, para toda rede já ter quem cadastre usuários.
    /// </summary>
    public partial class PerfilGestor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE usuarios SET ""Perfil"" = 2
                WHERE ""Id"" IN (
                    SELECT MIN(""Id"") FROM usuarios
                    WHERE ""ClienteId"" IS NOT NULL AND ""Ativo"" AND ""Perfil"" = 0
                    GROUP BY ""ClienteId"")
                AND ""ClienteId"" NOT IN (
                    SELECT ""ClienteId"" FROM usuarios WHERE ""Perfil"" = 2 AND ""ClienteId"" IS NOT NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE usuarios SET ""Perfil"" = 0 WHERE ""Perfil"" = 2;");
        }
    }
}
