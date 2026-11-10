using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Controllers;

/// <summary>
/// Alteração das empresas (CNPJs) de um cliente. O cadastro fica em ClientesController.
/// </summary>
[ApiController]
[Route("api/empresas")]
public class EmpresasController : ControllerBase
{
    private readonly PrevisioDbContext _db;
    private readonly ProtetorSegredos _protetor;

    public EmpresasController(PrevisioDbContext db, ProtetorSegredos protetor)
    {
        _db = db;
        _protetor = protetor;
    }

    /// <summary>
    /// Altera o nome, ativa/desativa a empresa e cadastra o aplicativo privado do Bling dela.
    /// O CNPJ não pode ser alterado.
    /// </summary>
    [HttpPut("{empresaId:int}")]
    public async Task<ActionResult<EmpresaDto>> Alterar(
        int empresaId, AlteracaoEmpresaDto dados, CancellationToken cancelamento)
    {
        Empresa? empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, cancelamento);
        if (empresa == null)
        {
            return NotFound("Empresa não encontrada.");
        }

        // Empresa de outro cliente responde igual a inexistente, para não revelar ids
        bool permitido = await ControleAcesso.PodeAcessarEmpresaAsync(_db, User, empresaId, cancelamento);
        if (!permitido)
        {
            return NotFound("Empresa não encontrada.");
        }

        if (dados.Nome != null)
        {
            if (string.IsNullOrWhiteSpace(dados.Nome))
            {
                return BadRequest("Informe o nome da empresa.");
            }
            empresa.Nome = dados.Nome.Trim();
        }

        if (dados.Ativo.HasValue)
        {
            empresa.Ativo = dados.Ativo.Value;
        }

        string clientIdAnterior = empresa.BlingClientId ?? "";

        if (dados.BlingClientId != null)
        {
            string clientId = dados.BlingClientId.Trim();
            if (clientId == "")
            {
                // Volta a usar o aplicativo global do Previsio
                empresa.BlingClientId = null;
                empresa.BlingClientSecretProtegido = null;
            }
            else
            {
                empresa.BlingClientId = clientId;
            }
        }

        if (!string.IsNullOrWhiteSpace(dados.BlingClientSecret) && !string.IsNullOrEmpty(empresa.BlingClientId))
        {
            empresa.BlingClientSecretProtegido = _protetor.Proteger(dados.BlingClientSecret.Trim());
        }

        if (!string.IsNullOrEmpty(empresa.BlingClientId) && string.IsNullOrEmpty(empresa.BlingClientSecretProtegido))
        {
            return BadRequest("Informe o Client Secret do aplicativo do Bling.");
        }

        // Os tokens pertencem ao aplicativo que autorizou: outro aplicativo exige conectar de novo
        if (clientIdAnterior != (empresa.BlingClientId ?? ""))
        {
            empresa.BlingAccessTokenProtegido = null;
            empresa.BlingRefreshTokenProtegido = null;
            empresa.BlingTokenExpiraEm = null;
        }

        await _db.SaveChangesAsync(cancelamento);
        return ClientesController.ParaDto(empresa);
    }
}
