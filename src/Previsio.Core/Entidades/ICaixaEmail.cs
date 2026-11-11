namespace Previsio.Core.Entidades;

/// <summary>
/// Caixa de e-mail (IMAP) de onde o Previsio lê os XMLs das notas.
/// A rede (Cliente) tem a sua; cada empresa pode ter uma própria.
/// </summary>
public interface ICaixaEmail
{
    string ImapHost { get; set; }
    int ImapPorta { get; set; }
    bool ImapSsl { get; set; }
    string ImapUsuario { get; set; }
    string ImapSenhaProtegida { get; set; }

    // Controle de leitura independente de "lido/não lido": UIDVALIDITY da caixa de entrada
    // e UID do último e-mail já processado (nulos até a primeira leitura)
    long? ImapUidValidity { get; set; }
    long? ImapUltimoUid { get; set; }
}
