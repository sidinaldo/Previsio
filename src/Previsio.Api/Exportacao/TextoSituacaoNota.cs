using Previsio.Core.Entidades;

namespace Previsio.Api.Exportacao;

/// <summary>
/// Textos oficiais das situações da nota (os mesmos das telas).
/// </summary>
public static class TextoSituacaoNota
{
    public static string Descrever(StatusNota status)
    {
        if (status == StatusNota.Recebida)
        {
            return "Aguardando lançamento";
        }
        if (status == StatusNota.Lancada)
        {
            return "Lançada no Bling";
        }
        if (status == StatusNota.LancadaManualmente)
        {
            return "Lançada manualmente";
        }
        if (status == StatusNota.SemParcelas)
        {
            return "Sem vencimentos no XML";
        }
        if (status == StatusNota.EmpresaNaoEncontrada)
        {
            return "CNPJ não cadastrado";
        }
        if (status == StatusNota.BlingDesconectado)
        {
            return "Bling desconectado";
        }
        if (status == StatusNota.Erro)
        {
            return "Erro ao lançar";
        }
        return status.ToString();
    }
}
