namespace Previsio.Core.Entidades;

/// <summary>
/// O que precisa de alguém no Previsio (sininho e e-mail de avisos).
/// </summary>
public enum TipoAviso
{
    // Nota que o Previsio não consegue lançar sozinho: CNPJ não cadastrado,
    // sem vencimentos no XML ou erro no Bling depois de todas as tentativas
    NotaComProblema = 0,

    // Empresa ativa sem conexão com o Bling: as notas dela ficam paradas
    BlingDesconectado = 1,

    // O Previsio não consegue entrar na caixa de e-mail (senha trocada, IMAP desligado)
    CaixaEmailFalhando = 2
}
