using Microsoft.EntityFrameworkCore;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Categorias;

/// <summary>
/// Categorias do Bling de cada empresa: guarda a lista (renovada uma vez por dia) e descobre,
/// aos poucos, a categoria de cada conta a pagar (a lista de contas do Bling não traz).
/// Quem chama salva.
/// </summary>
public class CategoriasBlingService
{
    // A lista de categorias muda pouco: busca de novo depois de um dia
    public static readonly TimeSpan RenovarLista = TimeSpan.FromDays(1);
    // Conta já conferida é conferida de novo depois de uma semana (alguém pode ter mudado a categoria no Bling)
    public static readonly TimeSpan RevisarConta = TimeSpan.FromDays(7);

    private readonly IRepositorio<CategoriaFinanceira> _categorias;
    private readonly BlingClient _bling;
    private readonly ILogger<CategoriasBlingService> _logger;

    public CategoriasBlingService(
        IRepositorio<CategoriaFinanceira> categorias,
        BlingClient bling,
        ILogger<CategoriasBlingService> logger)
    {
        _categorias = categorias;
        _bling = bling;
        _logger = logger;
    }

    /// <summary>
    /// Busca a lista de categorias da empresa quando não tem ou passou de um dia.
    /// Sem o escopo de categorias no aplicativo, deixa como está e segue.
    /// </summary>
    public async Task AtualizarSePrecisoAsync(Empresa empresa, string conta, string token, DateTime agoraUtc, CancellationToken cancelamento)
    {
        List<CategoriaFinanceira> guardadas = await _categorias.Consultar()
            .Where(c => c.EmpresaId == empresa.Id)
            .ToListAsync(cancelamento);

        bool precisa = guardadas.Count == 0 || guardadas.Min(c => c.AtualizadaEm) < agoraUtc - RenovarLista;
        if (!precisa)
        {
            return;
        }

        List<CategoriaBling> remotas;
        try
        {
            remotas = await _bling.ListarCategoriasAsync(conta, token, cancelamento);
        }
        catch (BlingException erro)
        {
            // A categoria é um detalhe: qualquer problema aqui não pode parar a sincronização das contas
            if (erro.SemPermissao)
            {
                _logger.LogWarning("Empresa {EmpresaId}: o Bling não liberou as categorias (reconectar com o escopo novo)", empresa.Id);
            }
            else
            {
                _logger.LogWarning(erro, "Empresa {EmpresaId}: não consegui buscar as categorias no Bling", empresa.Id);
            }
            return;
        }
        catch (System.Text.Json.JsonException erro)
        {
            _logger.LogWarning(erro, "Empresa {EmpresaId}: resposta inesperada do Bling nas categorias", empresa.Id);
            return;
        }

        var porId = new Dictionary<long, CategoriaFinanceira>();
        foreach (CategoriaFinanceira guardada in guardadas)
        {
            porId[guardada.BlingId] = guardada;
        }

        var recebidas = new HashSet<long>();
        foreach (CategoriaBling remota in remotas)
        {
            recebidas.Add(remota.Id);
            CategoriaFinanceira? categoria;
            if (!porId.TryGetValue(remota.Id, out categoria))
            {
                categoria = new CategoriaFinanceira();
                categoria.EmpresaId = empresa.Id;
                categoria.BlingId = remota.Id;
                _categorias.Adicionar(categoria);
            }
            categoria.BlingIdPai = remota.IdCategoriaPai;
            categoria.Descricao = Limitar(remota.Descricao.Trim());
            categoria.Tipo = remota.Tipo;
            categoria.AtualizadaEm = agoraUtc;
        }

        foreach (CategoriaFinanceira guardada in guardadas)
        {
            if (!recebidas.Contains(guardada.BlingId))
            {
                _categorias.Remover(guardada);
            }
        }
    }

    /// <summary>
    /// Abre no Bling até "maximo" contas para saber a categoria: primeiro as nunca conferidas
    /// (novas, ou que mudaram de valor, vencimento ou situação), depois as conferidas há mais de
    /// uma semana. Devolve quantas foram abertas.
    /// </summary>
    public async Task<int> ConferirCategoriasAsync(
        Empresa empresa,
        string conta,
        string token,
        List<ContaPagarSincronizada> contas,
        int maximo,
        DateTime agoraUtc,
        CancellationToken cancelamento)
    {
        DateTime limiteRevisao = agoraUtc - RevisarConta;
        List<ContaPagarSincronizada> aConferir = contas
            .Where(c => !c.CategoriaConferidaEm.HasValue || c.CategoriaConferidaEm.Value < limiteRevisao)
            .OrderBy(c => c.CategoriaConferidaEm.HasValue ? 1 : 0)
            .ThenBy(c => c.CategoriaConferidaEm)
            .ThenBy(c => c.Vencimento)
            .Take(maximo)
            .ToList();

        int abertas = 0;
        foreach (ContaPagarSincronizada contaLocal in aConferir)
        {
            ContaPagarBling? detalhe;
            try
            {
                detalhe = await _bling.ObterContaPagarAsync(conta, token, contaLocal.BlingId, cancelamento);
            }
            catch (BlingException erro)
            {
                if (erro.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Excluída no Bling depois da lista: a próxima sincronização remove
                    continue;
                }
                // Limite do Bling ou fora do ar: para por aqui, o resto fica para a próxima sincronização
                _logger.LogWarning(erro, "Empresa {EmpresaId}: parei de conferir categorias no Bling", empresa.Id);
                break;
            }
            catch (System.Text.Json.JsonException erro)
            {
                _logger.LogWarning(erro, "Empresa {EmpresaId}: resposta inesperada do Bling no detalhe da conta", empresa.Id);
                break;
            }
            abertas++;
            if (detalhe == null)
            {
                continue;
            }

            contaLocal.CategoriaBlingId = null;
            if (detalhe.Categoria != null && detalhe.Categoria.Id > 0)
            {
                contaLocal.CategoriaBlingId = detalhe.Categoria.Id;
            }
            contaLocal.CategoriaConferidaEm = agoraUtc;
        }
        return abertas;
    }

    private static string Limitar(string texto)
    {
        if (texto.Length > 200)
        {
            return texto.Substring(0, 200);
        }
        return texto;
    }
}
