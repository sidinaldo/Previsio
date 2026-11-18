using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Previsio.Data.Migrations
{
    /// <inheritdoc />
    public partial class ErpNeutro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Escrita à mão: o gerador do EF trocou colunas de lugar em contas_pagar (CategoriaBlingId
            // virava ErpContatoId) e apagava e recriava fornecedores_bling, perdendo os dados.
            migrationBuilder.RenameTable(
                name: "fornecedores_bling",
                newName: "fornecedores_erp");
            migrationBuilder.Sql("ALTER TABLE fornecedores_erp RENAME CONSTRAINT \"PK_fornecedores_bling\" TO \"PK_fornecedores_erp\";");
            migrationBuilder.Sql("ALTER TABLE fornecedores_erp RENAME CONSTRAINT \"FK_fornecedores_bling_empresas_EmpresaId\" TO \"FK_fornecedores_erp_empresas_EmpresaId\";");

            migrationBuilder.RenameColumn(
                name: "BlingClientId",
                table: "clientes",
                newName: "ErpClientId");
            migrationBuilder.RenameColumn(
                name: "BlingClientSecretProtegido",
                table: "clientes",
                newName: "ErpClientSecretProtegido");
            migrationBuilder.RenameColumn(
                name: "ConferirBlingAntesDeLancar",
                table: "clientes",
                newName: "ConferirErpAntesDeLancar");
            migrationBuilder.RenameColumn(
                name: "BlingClientId",
                table: "empresas",
                newName: "ErpClientId");
            migrationBuilder.RenameColumn(
                name: "BlingClientSecretProtegido",
                table: "empresas",
                newName: "ErpClientSecretProtegido");
            migrationBuilder.RenameColumn(
                name: "BlingAccessTokenProtegido",
                table: "empresas",
                newName: "ErpAccessTokenProtegido");
            migrationBuilder.RenameColumn(
                name: "BlingRefreshTokenProtegido",
                table: "empresas",
                newName: "ErpRefreshTokenProtegido");
            migrationBuilder.RenameColumn(
                name: "BlingTokenExpiraEm",
                table: "empresas",
                newName: "ErpTokenExpiraEm");
            migrationBuilder.RenameColumn(
                name: "BlingContaPagarId",
                table: "parcelas_nota",
                newName: "ErpContaPagarId");
            migrationBuilder.RenameColumn(
                name: "BlingId",
                table: "contas_pagar",
                newName: "ErpId");
            migrationBuilder.RenameColumn(
                name: "BlingContatoId",
                table: "contas_pagar",
                newName: "ErpContatoId");
            migrationBuilder.RenameColumn(
                name: "CategoriaBlingId",
                table: "contas_pagar",
                newName: "CategoriaErpId");
            migrationBuilder.RenameColumn(
                name: "FormaPagamentoBlingId",
                table: "contas_pagar",
                newName: "FormaPagamentoErpId");
            migrationBuilder.RenameColumn(
                name: "BlingId",
                table: "contas_receber",
                newName: "ErpId");
            migrationBuilder.RenameColumn(
                name: "BlingContatoId",
                table: "contas_receber",
                newName: "ErpContatoId");
            migrationBuilder.RenameColumn(
                name: "BlingId",
                table: "categorias_financeiras",
                newName: "ErpId");
            migrationBuilder.RenameColumn(
                name: "BlingIdPai",
                table: "categorias_financeiras",
                newName: "ErpIdPai");
            migrationBuilder.RenameColumn(
                name: "BlingId",
                table: "formas_pagamento",
                newName: "ErpId");
            migrationBuilder.RenameColumn(
                name: "BlingContatoId",
                table: "fornecedores_erp",
                newName: "ErpContatoId");

            migrationBuilder.RenameIndex(
                name: "IX_contas_pagar_EmpresaId_BlingId",
                table: "contas_pagar",
                newName: "IX_contas_pagar_EmpresaId_ErpId");
            migrationBuilder.RenameIndex(
                name: "IX_contas_receber_EmpresaId_BlingId",
                table: "contas_receber",
                newName: "IX_contas_receber_EmpresaId_ErpId");
            migrationBuilder.RenameIndex(
                name: "IX_categorias_financeiras_EmpresaId_BlingId",
                table: "categorias_financeiras",
                newName: "IX_categorias_financeiras_EmpresaId_ErpId");
            migrationBuilder.RenameIndex(
                name: "IX_formas_pagamento_EmpresaId_BlingId",
                table: "formas_pagamento",
                newName: "IX_formas_pagamento_EmpresaId_ErpId");
            migrationBuilder.RenameIndex(
                name: "IX_fornecedores_bling_EmpresaId_BlingContatoId",
                table: "fornecedores_erp",
                newName: "IX_fornecedores_erp_EmpresaId_ErpContatoId");

            // As empresas que já existem estão todas no Bling
            migrationBuilder.AddColumn<int>(
                name: "Erp",
                table: "empresas",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Erp",
                table: "empresas");

            migrationBuilder.RenameIndex(
                name: "IX_fornecedores_erp_EmpresaId_ErpContatoId",
                table: "fornecedores_erp",
                newName: "IX_fornecedores_bling_EmpresaId_BlingContatoId");
            migrationBuilder.RenameIndex(
                name: "IX_formas_pagamento_EmpresaId_ErpId",
                table: "formas_pagamento",
                newName: "IX_formas_pagamento_EmpresaId_BlingId");
            migrationBuilder.RenameIndex(
                name: "IX_categorias_financeiras_EmpresaId_ErpId",
                table: "categorias_financeiras",
                newName: "IX_categorias_financeiras_EmpresaId_BlingId");
            migrationBuilder.RenameIndex(
                name: "IX_contas_receber_EmpresaId_ErpId",
                table: "contas_receber",
                newName: "IX_contas_receber_EmpresaId_BlingId");
            migrationBuilder.RenameIndex(
                name: "IX_contas_pagar_EmpresaId_ErpId",
                table: "contas_pagar",
                newName: "IX_contas_pagar_EmpresaId_BlingId");

            migrationBuilder.RenameColumn(
                name: "ErpContatoId",
                table: "fornecedores_erp",
                newName: "BlingContatoId");
            migrationBuilder.RenameColumn(
                name: "ErpId",
                table: "formas_pagamento",
                newName: "BlingId");
            migrationBuilder.RenameColumn(
                name: "ErpIdPai",
                table: "categorias_financeiras",
                newName: "BlingIdPai");
            migrationBuilder.RenameColumn(
                name: "ErpId",
                table: "categorias_financeiras",
                newName: "BlingId");
            migrationBuilder.RenameColumn(
                name: "ErpContatoId",
                table: "contas_receber",
                newName: "BlingContatoId");
            migrationBuilder.RenameColumn(
                name: "ErpId",
                table: "contas_receber",
                newName: "BlingId");
            migrationBuilder.RenameColumn(
                name: "FormaPagamentoErpId",
                table: "contas_pagar",
                newName: "FormaPagamentoBlingId");
            migrationBuilder.RenameColumn(
                name: "CategoriaErpId",
                table: "contas_pagar",
                newName: "CategoriaBlingId");
            migrationBuilder.RenameColumn(
                name: "ErpContatoId",
                table: "contas_pagar",
                newName: "BlingContatoId");
            migrationBuilder.RenameColumn(
                name: "ErpId",
                table: "contas_pagar",
                newName: "BlingId");
            migrationBuilder.RenameColumn(
                name: "ErpContaPagarId",
                table: "parcelas_nota",
                newName: "BlingContaPagarId");
            migrationBuilder.RenameColumn(
                name: "ErpTokenExpiraEm",
                table: "empresas",
                newName: "BlingTokenExpiraEm");
            migrationBuilder.RenameColumn(
                name: "ErpRefreshTokenProtegido",
                table: "empresas",
                newName: "BlingRefreshTokenProtegido");
            migrationBuilder.RenameColumn(
                name: "ErpAccessTokenProtegido",
                table: "empresas",
                newName: "BlingAccessTokenProtegido");
            migrationBuilder.RenameColumn(
                name: "ErpClientSecretProtegido",
                table: "empresas",
                newName: "BlingClientSecretProtegido");
            migrationBuilder.RenameColumn(
                name: "ErpClientId",
                table: "empresas",
                newName: "BlingClientId");
            migrationBuilder.RenameColumn(
                name: "ConferirErpAntesDeLancar",
                table: "clientes",
                newName: "ConferirBlingAntesDeLancar");
            migrationBuilder.RenameColumn(
                name: "ErpClientSecretProtegido",
                table: "clientes",
                newName: "BlingClientSecretProtegido");
            migrationBuilder.RenameColumn(
                name: "ErpClientId",
                table: "clientes",
                newName: "BlingClientId");

            migrationBuilder.Sql("ALTER TABLE fornecedores_erp RENAME CONSTRAINT \"FK_fornecedores_erp_empresas_EmpresaId\" TO \"FK_fornecedores_bling_empresas_EmpresaId\";");
            migrationBuilder.Sql("ALTER TABLE fornecedores_erp RENAME CONSTRAINT \"PK_fornecedores_erp\" TO \"PK_fornecedores_bling\";");
            migrationBuilder.RenameTable(
                name: "fornecedores_erp",
                newName: "fornecedores_bling");
        }
    }
}
