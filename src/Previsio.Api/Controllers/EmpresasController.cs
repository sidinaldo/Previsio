using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Dtos;
using Previsio.Aplicacao.Empresas;
using Previsio.Aplicacao.Certificados;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Api.Controllers;

/// <summary>
/// Alteração das empresas (CNPJs) de um cliente. O cadastro fica em ClientesController.
/// </summary>
[ApiController]
[Route("api/empresas")]
public class EmpresasController : PrevisioControllerBase
{
    private readonly EmpresasService _empresas;
    private readonly CertificadosService _certificados;
    private readonly AcessoEmpresaService _acesso;

    public EmpresasController(EmpresasService empresas, CertificadosService certificados, AcessoEmpresaService acesso)
    {
        _empresas = empresas;
        _certificados = certificados;
        _acesso = acesso;
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

    /// <summary>
    /// Cadastra (ou troca) o certificado digital A1 da empresa: arquivo .pfx, senha e UF.
    /// Usado para buscar na SEFAZ o XML das notas que chegam só em PDF.
    /// </summary>
    [HttpPost("{empresaId:int}/certificado")]
    [RequestSizeLimit(200 * 1024)]
    public async Task<ActionResult<EmpresaDto>> SalvarCertificado(
        int empresaId, [FromForm] CertificadoEmpresaDto formulario, CancellationToken cancelamento)
    {
        IFormFile? arquivo = formulario.Arquivo;
        if (!await _acesso.PodeAcessarAsync(User, empresaId, cancelamento))
        {
            return NotFound(CertificadosService.MensagemEmpresaNaoEncontrada);
        }
        if (arquivo == null || arquivo.Length == 0)
        {
            return BadRequest(CertificadosService.MensagemArquivoVazio);
        }
        if (arquivo.Length > CertificadosService.TamanhoMaximoArquivo)
        {
            return BadRequest(CertificadosService.MensagemArquivoGrande);
        }
        byte[] pfx;
        using (var memoria = new MemoryStream())
        {
            await arquivo.CopyToAsync(memoria, cancelamento);
            pfx = memoria.ToArray();
        }
        return Responder(await _certificados.SalvarAsync(empresaId, pfx, formulario.Senha ?? "", formulario.Uf ?? "", cancelamento));
    }

    /// <summary>Liga ou desliga a busca completa das NF-e da empresa na SEFAZ.</summary>
    [HttpPut("{empresaId:int}/sefaz")]
    public async Task<ActionResult<EmpresaDto>> AlterarBuscaSefaz(int empresaId, BuscaSefazDto dados, CancellationToken cancelamento)
    {
        if (!await _acesso.PodeAcessarAsync(User, empresaId, cancelamento))
        {
            return NotFound(CertificadosService.MensagemEmpresaNaoEncontrada);
        }
        return Responder(await _certificados.AlterarBuscaSefazAsync(empresaId, dados.Ativa, cancelamento));
    }

    [HttpDelete("{empresaId:int}/certificado")]
    public async Task<ActionResult<EmpresaDto>> RemoverCertificado(int empresaId, CancellationToken cancelamento)
    {
        if (!await _acesso.PodeAcessarAsync(User, empresaId, cancelamento))
        {
            return NotFound(CertificadosService.MensagemEmpresaNaoEncontrada);
        }
        return Responder(await _certificados.RemoverAsync(empresaId, cancelamento));
    }
}
