namespace Previsio.Core.Entidades;

/// <summary>
/// Registro de uma tentativa que falhou ao ler uma nota do e-mail ou ao lançá-la no Bling.
/// Guarda uma cópia dos dados da nota no momento da falha, para o histórico
/// continuar correto mesmo que a nota mude depois.
/// </summary>
public class FalhaProcessamento
{
    public int Id { get; set; }
    public DateTime OcorridaEm { get; set; } = DateTime.UtcNow;

    public int ClienteId { get; set; }
    public int? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public int? NotaFiscalId { get; set; }

    public EtapaFalha Etapa { get; set; }

    // Por que falhou
    public string Mensagem { get; set; } = "";

    // Qual endereço tentou: "GET", "POST" ou "IMAP" + URL completa ou servidor de e-mail
    public string Metodo { get; set; } = "";
    public string Endereco { get; set; } = "";
    public int? StatusHttp { get; set; }

    // Resposta devolvida pelo Bling (cortada para não crescer demais)
    public string? Resposta { get; set; }

    // Qual nota tentou registrar (cópia no momento da falha)
    public string? NumeroNota { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? Fornecedor { get; set; }
    public string? CnpjFornecedor { get; set; }
    public decimal? ValorNota { get; set; }

    // Parcela que estava sendo lançada, quando a falha foi na conta a pagar
    public string? NumeroParcela { get; set; }
    public decimal? ValorParcela { get; set; }
    public DateTime? VencimentoParcela { get; set; }

    // E-mail de onde a nota veio (remetente | assunto) ou nome do anexo
    public string? Origem { get; set; }

    // UID do e-mail na caixa, quando a falha foi ao ler um e-mail específico
    // (conta as tentativas para pular um e-mail que falha sempre)
    public long? UidEmail { get; set; }
}
