namespace Previsio.Core.Entidades;

/// <summary>
/// Cada CNPJ do cliente. Cada empresa tem a sua própria conta no Bling.
/// </summary>
public class Empresa : ICaixaEmail
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string Nome { get; set; } = "";
    public string Cnpj { get; set; } = "";

    // Caixa de e-mail própria da empresa (opcional). Vazia: as notas dela chegam pela caixa da rede.
    // Com caixa própria, as duas são lidas; a nota vai para a empresa pelo CNPJ do destinatário.
    public string ImapHost { get; set; } = "";
    public int ImapPorta { get; set; } = 993;
    public bool ImapSsl { get; set; } = true;
    public string ImapUsuario { get; set; } = "";
    public string ImapSenhaProtegida { get; set; } = "";
    public long? ImapUidValidity { get; set; }
    public long? ImapUltimoUid { get; set; }
    public DateTime? ImapUltimaConexaoEm { get; set; }
    public DateTime? ImapUltimaFalhaConexaoEm { get; set; }
    public string? ImapUltimoErroConexao { get; set; }

    // Aplicativo privado criado dentro da conta Bling desta empresa.
    // Vazio: usa o aplicativo da rede (Cliente) e, se a rede também não tiver, o global do Previsio.
    public string? BlingClientId { get; set; }
    public string? BlingClientSecretProtegido { get; set; }

    // Tokens OAuth do Bling (criptografados com Data Protection)
    public string? BlingAccessTokenProtegido { get; set; }
    public string? BlingRefreshTokenProtegido { get; set; }
    public DateTime? BlingTokenExpiraEm { get; set; }

    public DateTime? UltimaSincronizacaoEm { get; set; }

    // Certificado digital A1 (.pfx) da empresa, para buscar na SEFAZ o XML das notas que chegam só em PDF.
    // O arquivo e a senha ficam criptografados com Data Protection.
    public string? CertificadoPfxProtegido { get; set; }
    public string? CertificadoSenhaProtegida { get; set; }
    public string? CertificadoTitular { get; set; }
    public DateTime? CertificadoValidoAte { get; set; }
    // Sigla da UF da empresa (SP, RN...), exigida pela SEFAZ na consulta
    public string? Uf { get; set; }

    // Busca completa na SEFAZ (distribuição por NSU): toda NF-e emitida contra o CNPJ, mesmo a que
    // não chega por e-mail. Liga sozinha quando há certificado (da empresa ou da matriz); dá para desligar.
    public bool SefazBuscaAtiva { get; set; } = true;
    // Último NSU lido; nulo = a busca ainda não começou (a primeira pula o histórico)
    public string? SefazUltimoNsu { get; set; }
    // Antes disso a SEFAZ não é consultada (ela bloqueia por 1 hora quem consulta sem nada novo)
    public DateTime? SefazProximaBuscaEm { get; set; }
    public DateTime? SefazUltimaBuscaEm { get; set; }
    // Resultado da última busca, para a tela ("3 notas novas", "certificado vencido"...)
    public string? SefazSituacao { get; set; }

    public bool Ativo { get; set; } = true;

    public bool BlingConectado
    {
        get { return !string.IsNullOrEmpty(BlingRefreshTokenProtegido); }
    }

    public bool TemCertificado
    {
        get { return !string.IsNullOrEmpty(CertificadoPfxProtegido) && !string.IsNullOrEmpty(CertificadoSenhaProtegida); }
    }

    public bool TemEmailProprio
    {
        get { return !string.IsNullOrWhiteSpace(ImapHost) && !string.IsNullOrWhiteSpace(ImapUsuario); }
    }
}
