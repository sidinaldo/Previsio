using System.Linq.Expressions;
using Previsio.Core.Entidades;
using Previsio.Core.Comum;

namespace Previsio.Core.Contratos;

/// <summary>
/// Regras do teste grátis: 15 dias contando o dia de hoje (horário de Brasília).
/// A rede em teste que passou do prazo deixa de ser processada pelos jobs.
/// </summary>
public static class PeriodoTeste
{
    public const int DiasPadrao = 15;
    public const int DiasAvisoFinal = 3;

    /// <summary>
    /// Redes que os jobs processam: ativas e contratadas, ou em teste dentro do prazo. EF traduz para SQL.
    /// </summary>
    public static Expression<Func<Cliente, bool>> Processa(DateTime agoraUtc)
    {
        return c => c.Ativo
            && (c.Contrato == SituacaoContrato.Contratado || (c.TesteTerminaEm != null && c.TesteTerminaEm > agoraUtc));
    }

    public static bool Encerrado(Cliente cliente, DateTime agoraUtc)
    {
        if (cliente.Contrato != SituacaoContrato.EmTeste)
        {
            return false;
        }
        return !cliente.TesteTerminaEm.HasValue || cliente.TesteTerminaEm.Value <= agoraUtc;
    }

    /// <summary>
    /// Fim do teste para N dias a partir de um dia (inclusive): início do dia seguinte ao último, em UTC.
    /// </summary>
    public static DateTime Termino(DateTime diaInicialBrasil, int dias)
    {
        return RelogioBrasil.InicioDoDiaEmUtc(diaInicialBrasil.Date.AddDays(dias));
    }

    /// <summary>
    /// Último dia do teste (data de Brasília), para a tela: "até 25/11".
    /// </summary>
    public static DateTime UltimoDia(DateTime terminoUtc)
    {
        DateTime termino = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(terminoUtc, DateTimeKind.Utc), RelogioBrasil.Fuso);
        return termino.Date.AddDays(-1);
    }

    /// <summary>
    /// Dias que faltam contando hoje (1 = último dia); 0 quando já acabou.
    /// </summary>
    public static int DiasRestantes(DateTime terminoUtc, DateTime agoraUtc)
    {
        if (terminoUtc <= agoraUtc)
        {
            return 0;
        }
        DateTime hoje = HojeBrasil(agoraUtc);
        return (UltimoDia(terminoUtc) - hoje).Days + 1;
    }

    public static DateTime HojeBrasil(DateTime agoraUtc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(agoraUtc, DateTimeKind.Utc), RelogioBrasil.Fuso).Date;
    }

    /// <summary>
    /// "Contratado", "EmTeste" ou "TesteEncerrado", para a tela.
    /// </summary>
    public static string Situacao(Cliente cliente, DateTime agoraUtc)
    {
        if (cliente.Contrato == SituacaoContrato.Contratado)
        {
            return "Contratado";
        }
        if (Encerrado(cliente, agoraUtc))
        {
            return "TesteEncerrado";
        }
        return "EmTeste";
    }
}
