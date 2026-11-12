using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.NotasSoPdf;

/// <summary>
/// Uma consulta à SEFAZ: a loja interessada (destinatária possível), de quem é o certificado e a UF.
/// </summary>
public class ConsultaSefaz
{
    public Empresa Empresa { get; set; } = null!;
    public Empresa DonoDoCertificado { get; set; } = null!;
    public string Uf { get; set; } = "";
}
