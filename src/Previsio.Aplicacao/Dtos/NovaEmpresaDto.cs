namespace Previsio.Aplicacao.Dtos;

public class NovaEmpresaDto
{
    public string Nome { get; set; } = "";
    public string Cnpj { get; set; } = "";

    // Opcionais: aplicativo privado criado na conta Bling desta empresa
    public string? BlingClientId { get; set; }
    public string? BlingClientSecret { get; set; }

    // Opcionais: caixa de e-mail própria da empresa (sem ela, as notas chegam pela caixa da rede)
    public string? ImapHost { get; set; }
    public int? ImapPorta { get; set; }
    public bool? ImapSsl { get; set; }
    public string? ImapUsuario { get; set; }
    public string? ImapSenha { get; set; }
}
