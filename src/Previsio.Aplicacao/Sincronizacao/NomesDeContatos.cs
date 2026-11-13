namespace Previsio.Aplicacao.Sincronizacao;

/// <summary>
/// Nomes dos contatos do Bling (id → nome) e quantos foram buscados no Bling agora.
/// </summary>
public class NomesDeContatos
{
    public Dictionary<long, string> Nomes { get; } = new Dictionary<long, string>();
    public int Buscas { get; set; }
}
