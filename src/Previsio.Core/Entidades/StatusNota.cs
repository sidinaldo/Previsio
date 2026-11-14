namespace Previsio.Core.Entidades;

public enum StatusNota
{
    Recebida = 0,
    Lancada = 1,
    SemParcelas = 2,
    EmpresaNaoEncontrada = 3,
    BlingDesconectado = 4,
    Erro = 5,

    // Alguém resolveu o problema e lançou a conta a pagar direto no Bling
    LancadaManualmente = 6,

    // O emitente cancelou a nota na SEFAZ antes de ela ir para o Bling: não é lançada
    CanceladaNaSefaz = 7
}
