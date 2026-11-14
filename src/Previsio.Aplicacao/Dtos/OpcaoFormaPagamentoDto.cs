namespace Previsio.Aplicacao.Dtos;

public class OpcaoFormaPagamentoDto
{
    public string Nome { get; set; } = "";
    // Grupo do Previsio (boleto, pix, debito-automatico...) e o nome dele
    public string Grupo { get; set; } = "";
    public string GrupoNome { get; set; } = "";
    public bool SaiSozinho { get; set; }
}
