using Previsio.Core.Pagamento;

namespace Previsio.Core.Entidades;

/// <summary>
/// Forma de pagamento do Bling de uma empresa (Boleto, Pix, Débito automático...), guardada para o
/// lançamento (achar a forma pelo nome ou pelo tipo da nota) e para as telas (nome e grupo de cada conta).
/// Cada conta Bling tem as suas formas, com ids próprios.
/// </summary>
public class FormaPagamento
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public long BlingId { get; set; }
    public string Descricao { get; set; } = "";
    // Código do meio de pagamento da NF-e (tPag): 15 = boleto, 17 = Pix, 99 = outros...
    public int TipoPagamento { get; set; }
    // 1 = ativa no Bling
    public int Situacao { get; set; }
    // Grupo do Previsio (GruposFormaPagamento): boleto, pix, debito-automatico...
    public string Grupo { get; set; } = GruposFormaPagamento.Outras;

    public DateTime AtualizadaEm { get; set; } = DateTime.UtcNow;
}
