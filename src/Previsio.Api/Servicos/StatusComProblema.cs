using Previsio.Core.Entidades;

namespace Previsio.Api.Servicos;

/// <summary>
/// Situações em que a nota precisa de alguém para ser resolvida
/// (as pendentes, menos a que só está aguardando o lançamento automático).
/// Só estas podem ser marcadas como lançadas manualmente no Bling.
/// </summary>
public static class StatusComProblema
{
    public static readonly StatusNota[] Todos = new StatusNota[]
    {
        StatusNota.SemParcelas,
        StatusNota.EmpresaNaoEncontrada,
        StatusNota.BlingDesconectado,
        StatusNota.Erro
    };
}
