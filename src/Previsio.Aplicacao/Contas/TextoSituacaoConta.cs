using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Contas;

/// <summary>
/// Nome em português da situação da conta no Bling.
/// </summary>
public static class TextoSituacaoConta
{
    public static string De(int situacao)
    {
        if (situacao == SituacaoConta.EmAberto)
        {
            return "Em aberto";
        }
        if (situacao == SituacaoConta.Pago)
        {
            return "Paga";
        }
        if (situacao == SituacaoConta.ParcialmentePago)
        {
            return "Parcialmente paga";
        }
        if (situacao == SituacaoConta.Devolvido)
        {
            return "Devolvida";
        }
        if (situacao == SituacaoConta.Cancelado)
        {
            return "Cancelada";
        }
        return "Situação " + situacao;
    }

    /// <summary>A mesma situação, falando de conta a receber ("Recebida" no lugar de "Paga").</summary>
    public static string DeReceber(int situacao)
    {
        if (situacao == SituacaoConta.Pago)
        {
            return "Recebida";
        }
        if (situacao == SituacaoConta.ParcialmentePago)
        {
            return "Parcialmente recebida";
        }
        return De(situacao);
    }

    public static bool EmAberto(int situacao)
    {
        return situacao == SituacaoConta.EmAberto || situacao == SituacaoConta.ParcialmentePago;
    }
}
