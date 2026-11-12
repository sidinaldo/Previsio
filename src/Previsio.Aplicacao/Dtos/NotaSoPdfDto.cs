namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Nota que chegou por e-mail só em PDF e ainda precisa do XML (ou de alguém resolver).
/// </summary>
public class NotaSoPdfDto
{
    public int Id { get; set; }
    public DateTime RecebidaEm { get; set; }
    public string Remetente { get; set; } = "";
    public string NomeRemetente { get; set; } = "";
    public string Assunto { get; set; } = "";
    public string PdfNome { get; set; } = "";
    // Caixa da empresa em que chegou (nula: caixa da rede)
    public string? EmpresaCaixaNome { get; set; }

    public string? ChaveAcesso { get; set; }
    public string? OrigemChave { get; set; }
    // Tirados da chave: número da nota e CNPJ do fornecedor
    public string? NumeroNota { get; set; }
    public string? CnpjFornecedor { get; set; }

    // BuscandoNaSefaz, SemChave, SemCertificado, NaoResolvida
    public string Situacao { get; set; } = "";
    public string? Motivo { get; set; }
    public int TentativasSefaz { get; set; }
    public DateTime? ProximaTentativaEm { get; set; }
    public DateTime? CienciaEnviadaEm { get; set; }
    public DateTime? XmlPedidoEm { get; set; }
}
