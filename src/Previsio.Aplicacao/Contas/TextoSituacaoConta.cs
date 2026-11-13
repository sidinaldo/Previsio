using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Contas;

/// <summary>
/// Nome em português da situação da conta no Bling.
/// </summary>
public static class TextoSituacaoConta
{
    public static string De(int situacao)
    {
        if (situacao == SituacaoContaBling.EmAberto)
        {
            return "Em aberto";
        }
        if (situacao == SituacaoContaBling.Pago)
        {
            return "Paga";
        }
        if (situacao == SituacaoContaBling.ParcialmentePago)
        {
            return "Parcialmente paga";
        }
        if (situacao == SituacaoContaBling.Devolvido)
        {
            return "Devolvida";
        }
        if (situacao == SituacaoContaBling.Cancelado)
        {
            return "Cancelada";
        }
        return "Situação " + situacao;
    }

    /// <summary>A mesma situação, falando de conta a receber ("Recebida" no lugar de "Paga").</summary>
    public static string DeReceber(int situacao)
    {
        if (situacao == SituacaoContaBling.Pago)
        {
            return "Recebida";
        }
        if (situacao == SituacaoContaBling.ParcialmentePago)
        {
            return "Parcialmente recebida";
        }
        return De(situacao);
    }

    public static bool EmAberto(int situacao)
    {
        return situacao == SituacaoContaBling.EmAberto || situacao == SituacaoContaBling.ParcialmentePago;
    }
}
