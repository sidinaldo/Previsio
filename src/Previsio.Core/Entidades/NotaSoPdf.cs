namespace Previsio.Core.Entidades;

/// <summary>
/// E-mail que trouxe a nota fiscal só em PDF (DANFE), sem o XML. Fica pendente até o XML
/// chegar (SEFAZ ou anexado à mão) ou até alguém lançar a conta direto no Bling.
/// </summary>
public class NotaSoPdf
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    // Empresa dona da caixa em que o e-mail chegou (nula: caixa da rede)
    public int? EmpresaCaixaId { get; set; }
    public Empresa? EmpresaCaixa { get; set; }

    // Message-Id do e-mail, para não registrar o mesmo e-mail duas vezes
    public string MensagemId { get; set; } = "";
    public string Remetente { get; set; } = "";
    public string NomeRemetente { get; set; } = "";
    public string Assunto { get; set; } = "";
    public string OrigemEmail { get; set; } = "";
    public DateTime RecebidaEm { get; set; } = DateTime.UtcNow;

    public string PdfNome { get; set; } = "";
    public byte[] Pdf { get; set; } = Array.Empty<byte>();

    // Chave de acesso de 44 dígitos e onde ela foi achada ("nome do arquivo", "PDF", "assunto", "informada")
    public string? ChaveAcesso { get; set; }
    public string? OrigemChave { get; set; }

    public SituacaoNotaSoPdf Situacao { get; set; } = SituacaoNotaSoPdf.BuscandoNaSefaz;
    public string? Motivo { get; set; }

    // Busca na SEFAZ
    public int TentativasSefaz { get; set; }
    public DateTime? ProximaTentativaEm { get; set; }
    public DateTime? CienciaEnviadaEm { get; set; }
    // Loja que a SEFAZ confirmou como destinatária (a da ciência da operação)
    public int? EmpresaDestinoId { get; set; }

    public DateTime? XmlPedidoEm { get; set; }

    public int? NotaFiscalId { get; set; }
    public DateTime? ResolvidaEm { get; set; }
    public string? ResolvidaPor { get; set; }
    public string? Observacao { get; set; }
}
