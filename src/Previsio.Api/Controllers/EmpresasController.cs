using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;

namespace Previsio.Api.Controllers;

/// <summary>
/// Alteração das empresas (CNPJs) de um cliente. O cadastro fica em ClientesController.
/// </summary>
[ApiController]
[Route("api/empresas")]
public class EmpresasController : PrevisioControllerBase
{
    private readonly EmpresasService _empresas;

    public EmpresasController(EmpresasService empresas)
    {
        _empresas = empresas;
    }

    /// <summary>
    /// Altera o nome, ativa/desativa a empresa e cadastra o aplicativo privado do Bling dela.
    /// O CNPJ não pode ser alterado.
    /// </summary>
    [HttpPut("{empresaId:int}")]
    public async Task<ActionResult<EmpresaDto>> Alterar(
        int empresaId, AlteracaoEmpresaDto dados, CancellationToken cancelamento)
    {
        return Responder(await _empresas.AlterarAsync(empresaId, dados, User, cancelamento));
    }
}
