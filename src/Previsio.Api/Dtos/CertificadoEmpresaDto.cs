using Microsoft.AspNetCore.Http;

namespace Previsio.Api.Dtos;

/// <summary>
/// Formulário do certificado A1 (multipart): arquivo .pfx, senha e UF da empresa.
/// Numa classe só para o Swagger montar o upload (com [FromForm] em IFormFile solto ele falha).
/// </summary>
public class CertificadoEmpresaDto
{
    public IFormFile? Arquivo { get; set; }
    public string? Senha { get; set; }
    public string? Uf { get; set; }
}
