namespace Previsio.Core.Entidades;

/// <summary>
/// Nota fiscal lida do e-mail. A chave de acesso garante que a mesma nota
/// nunca seja lançada duas vezes, mesmo que chegue em mais de um e-mail.
/// </summary>
public class NotaFiscal
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public int? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public string ChaveAcesso { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Serie { get; set; } = "";
    public DateTime DataEmissao { get; set; }
    public decimal ValorTotal { get; set; }

    public string CnpjDestinatario { get; set; } = "";
    public string CnpjFornecedor { get; set; } = "";
    public string NomeFornecedor { get; set; } = "";
    // Meio de pagamento do XML (tPag de <pag>): 15 = boleto, 17 = Pix...; nulo quando a nota não diz
    public int? TipoPagamento { get; set; }

    public StatusNota Status { get; set; } = StatusNota.Recebida;
    public string? MensagemErro { get; set; }
    public int Tentativas { get; set; }

    public string OrigemEmail { get; set; } = "";

    // Empresa dona da caixa de e-mail em que a nota chegou; nula quando chegou na caixa da rede.
    // A nota vai para a empresa pelo CNPJ do XML; isto só serve para avisar quando as duas não batem.
    public int? EmpresaCaixaId { get; set; }
    public Empresa? EmpresaCaixa { get; set; }
    public DateTime RecebidaEm { get; set; } = DateTime.UtcNow;
    public DateTime? LancadaEm { get; set; }

    // Marcação de "resolvida e lançada manualmente no Bling": quem, quando e observação
    public DateTime? ResolvidaManualmenteEm { get; set; }
    public int? ResolvidaManualmentePorId { get; set; }
    public string? ResolvidaManualmentePor { get; set; }
    public string? ObservacaoResolucao { get; set; }

    public List<ParcelaNota> Parcelas { get; set; } = new List<ParcelaNota>();
}
