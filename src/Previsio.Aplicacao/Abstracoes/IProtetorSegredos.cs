namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Criptografa os segredos gravados no banco (tokens do Bling, senhas de IMAP e do certificado).
/// </summary>
public interface IProtetorSegredos
{
    string Proteger(string valor);

    string Revelar(string valorProtegido);
}
