namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Resposta do teste da caixa: se conectou e entrou, e o motivo quando não.
/// </summary>
public class ResultadoTesteEmailDto
{
    public bool Conectou { get; set; }
    public string Mensagem { get; set; } = "";
}
