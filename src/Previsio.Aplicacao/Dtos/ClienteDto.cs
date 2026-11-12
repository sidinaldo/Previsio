namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Dados do cliente para as telas. A senha do IMAP nunca sai da API.
/// </summary>
public class ClienteDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string ImapHost { get; set; } = "";
    public int ImapPorta { get; set; }
    public bool ImapSsl { get; set; }
    public string ImapUsuario { get; set; } = "";

    // Aplicativo privado do Bling da rede. O secret nunca é devolvido, só se está cadastrado.
    public string? BlingClientId { get; set; }
    public bool BlingClientSecretCadastrado { get; set; }

    // "Contratado", "EmTeste" ou "TesteEncerrado"
    public string Contrato { get; set; } = "Contratado";
    // Último dia do teste grátis (data de Brasília) e quantos dias faltam contando hoje
    public DateTime? TesteUltimoDia { get; set; }
    public int? DiasRestantesTeste { get; set; }

    public bool Ativo { get; set; }
}
