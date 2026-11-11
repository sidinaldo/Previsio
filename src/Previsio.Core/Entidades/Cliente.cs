namespace Previsio.Core.Entidades;

/// <summary>
/// Cliente do Previsio (tenant, a rede). Ex.: o grupo de autopeças com 5 CNPJs.
/// </summary>
public class Cliente : ICaixaEmail
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";

    // Caixa de e-mail dedicada que recebe os XMLs encaminhados pelo cliente
    public string ImapHost { get; set; } = "";
    public int ImapPorta { get; set; } = 993;
    public bool ImapSsl { get; set; } = true;
    public string ImapUsuario { get; set; } = "";
    public string ImapSenhaProtegida { get; set; } = "";

    // Controle de leitura independente de "lido/não lido": UIDVALIDITY da caixa de entrada
    // e UID do último e-mail já processado (nulos até a primeira leitura)
    public long? ImapUidValidity { get; set; }
    public long? ImapUltimoUid { get; set; }

    // Aplicativo privado do Bling da rede: vale para as empresas que não têm um próprio.
    // Vazio: as empresas sem aplicativo próprio usam o aplicativo global do Previsio (appsettings).
    public string? BlingClientId { get; set; }
    public string? BlingClientSecretProtegido { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<Empresa> Empresas { get; set; } = new List<Empresa>();
}
