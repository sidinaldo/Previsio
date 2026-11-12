namespace Previsio.Api.Dtos;

/// <summary>
/// Resposta do "Tratar como nota em PDF".
/// </summary>
public class TratamentoEmailIgnoradoDto
{
    public string Mensagem { get; set; } = "";
    public EmailIgnoradoDto Email { get; set; } = new EmailIgnoradoDto();
}
