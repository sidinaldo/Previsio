using Previsio.Core.Entidades;

namespace Previsio.Api.Servicos;

/// <summary>
/// Situações de nota que ainda não virou conta a pagar no Bling (painel e tela de notas pendentes).
/// </summary>
public static class StatusPendentes
{
    public static readonly StatusNota[] Todos = new StatusNota[]
    {
        StatusNota.Recebida,
        StatusNota.SemParcelas,
        StatusNota.EmpresaNaoEncontrada,
        StatusNota.BlingDesconectado,
        StatusNota.Erro
    };
}
