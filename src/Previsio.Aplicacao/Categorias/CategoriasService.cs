using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Aplicacao.Categorias;

/// <summary>
/// Configuração das categorias da rede: categoria padrão e regras "fornecedor → categoria".
/// As categorias são escolhidas pelo nome; o card avisa em quais empresas o nome não existe no Bling.
/// </summary>
public class CategoriasService
{
    public const string MensagemRegraNaoEncontrada = "Regra não encontrada.";
    private const int MaximoFornecedores = 500;

    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<CategoriaFinanceira> _categorias;
    private readonly IRepositorio<RegraCategoria> _regras;
    private readonly IRepositorio<NotaFiscal> _notas;
    private readonly CategoriasOptions _opcoes;

    public CategoriasService(
        IRepositorio<Cliente> clientes,
        IRepositorio<Empresa> empresas,
        IRepositorio<CategoriaFinanceira> categorias,
        IRepositorio<RegraCategoria> regras,
        IRepositorio<NotaFiscal> notas,
        IOptions<CategoriasOptions> opcoes)
    {
        _opcoes = opcoes.Value;
        _clientes = clientes;
        _empresas = empresas;
        _categorias = categorias;
        _regras = regras;
        _notas = notas;
    }

    public async Task<Resultado<ConfiguracaoCategoriasDto>> ObterAsync(int clienteId, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.ConsultarSomenteLeitura().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ConfiguracaoCategoriasDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }

        CategoriasDaRede rede = await CarregarRedeAsync(clienteId, cancelamento);
        var dto = new ConfiguracaoCategoriasDto();
        dto.CategoriaPadrao = cliente.CategoriaPadrao;
        dto.PadraoAutomatico = _opcoes.PadraoAutomaticoValido();
        dto.Opcoes = rede.Opcoes();
        dto.EmpresasSemCategorias = rede.EmpresasSemCategorias();
        // A que vale de fato: a escolhida pela rede ou, sem ela, a automática
        string? padraoEmUso = cliente.CategoriaPadrao;
        if (string.IsNullOrWhiteSpace(padraoEmUso))
        {
            padraoEmUso = dto.PadraoAutomatico;
        }
        if (padraoEmUso != null)
        {
            dto.PadraoAusenteEm = rede.AusenteEm(padraoEmUso);
        }

        List<RegraCategoria> regras = await _regras.ConsultarSomenteLeitura()
            .Where(r => r.ClienteId == clienteId)
            .ToListAsync(cancelamento);
        List<FornecedorDaRedeDto> fornecedores = await FornecedoresAsync(clienteId, cancelamento);
        var nomesFornecedores = new Dictionary<string, string>();
        foreach (FornecedorDaRedeDto fornecedor in fornecedores)
        {
            nomesFornecedores[fornecedor.Cnpj] = fornecedor.Nome;
        }

        foreach (RegraCategoria regra in regras)
        {
            var item = new RegraCategoriaDto();
            item.Id = regra.Id;
            item.CnpjFornecedor = regra.CnpjFornecedor;
            string? nome;
            if (nomesFornecedores.TryGetValue(regra.CnpjFornecedor, out nome))
            {
                item.FornecedorNome = nome;
            }
            item.Categoria = regra.Categoria;
            item.AusenteEm = rede.AusenteEm(regra.Categoria);
            dto.Regras.Add(item);
        }
        dto.Regras = dto.Regras.OrderBy(r => r.FornecedorNome ?? r.CnpjFornecedor).ToList();
        dto.Fornecedores = fornecedores;
        return Resultado<ConfiguracaoCategoriasDto>.Ok(dto);
    }

    public async Task<Resultado<ConfiguracaoCategoriasDto>> AlterarPadraoAsync(
        int clienteId, CategoriaPadraoDto dados, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ConfiguracaoCategoriasDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }

        string categoria = (dados.Categoria ?? "").Trim();
        if (categoria == "")
        {
            cliente.CategoriaPadrao = null;
        }
        else
        {
            CategoriasDaRede rede = await CarregarRedeAsync(clienteId, cancelamento);
            string? nome = rede.NomeExistente(categoria);
            if (nome == null)
            {
                return Resultado<ConfiguracaoCategoriasDto>.Invalido(MensagemCategoriaInexistente(categoria));
            }
            cliente.CategoriaPadrao = nome;
        }
        await _clientes.SalvarAsync(cancelamento);
        return await ObterAsync(clienteId, cancelamento);
    }

    public async Task<Resultado<ConfiguracaoCategoriasDto>> SalvarRegraAsync(
        int clienteId, NovaRegraCategoriaDto dados, CancellationToken cancelamento)
    {
        bool existe = await _clientes.ExisteAsync(c => c.Id == clienteId, cancelamento);
        if (!existe)
        {
            return Resultado<ConfiguracaoCategoriasDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }

        string cnpj = RegrasCategoria.ApenasDigitos(dados.CnpjFornecedor);
        if (cnpj.Length != 11 && cnpj.Length != 14)
        {
            return Resultado<ConfiguracaoCategoriasDto>.Invalido("Informe o CNPJ (ou CPF) do fornecedor.");
        }
        string categoria = (dados.Categoria ?? "").Trim();
        if (categoria == "")
        {
            return Resultado<ConfiguracaoCategoriasDto>.Invalido("Escolha a categoria.");
        }
        CategoriasDaRede rede = await CarregarRedeAsync(clienteId, cancelamento);
        string? nome = rede.NomeExistente(categoria);
        if (nome == null)
        {
            return Resultado<ConfiguracaoCategoriasDto>.Invalido(MensagemCategoriaInexistente(categoria));
        }

        // Uma regra por fornecedor: se já existe, troca a categoria
        RegraCategoria? regra = await _regras.Consultar()
            .FirstOrDefaultAsync(r => r.ClienteId == clienteId && r.CnpjFornecedor == cnpj, cancelamento);
        if (regra == null)
        {
            regra = new RegraCategoria();
            regra.ClienteId = clienteId;
            regra.CnpjFornecedor = cnpj;
            _regras.Adicionar(regra);
        }
        regra.Categoria = nome;
        await _regras.SalvarAsync(cancelamento);
        return await ObterAsync(clienteId, cancelamento);
    }

    public async Task<Resultado<ConfiguracaoCategoriasDto>> RemoverRegraAsync(
        int clienteId, int regraId, CancellationToken cancelamento)
    {
        RegraCategoria? regra = await _regras.Consultar()
            .FirstOrDefaultAsync(r => r.Id == regraId && r.ClienteId == clienteId, cancelamento);
        if (regra == null)
        {
            return Resultado<ConfiguracaoCategoriasDto>.NaoEncontrado(MensagemRegraNaoEncontrada);
        }
        _regras.Remover(regra);
        await _regras.SalvarAsync(cancelamento);
        return await ObterAsync(clienteId, cancelamento);
    }

    private static string MensagemCategoriaInexistente(string categoria)
    {
        return "A categoria \"" + categoria + "\" não existe no Bling de nenhuma empresa da rede.";
    }

    private async Task<CategoriasDaRede> CarregarRedeAsync(int clienteId, CancellationToken cancelamento)
    {
        List<Empresa> empresas = await _empresas.ConsultarSomenteLeitura()
            .Where(e => e.ClienteId == clienteId && e.Ativo && e.BlingRefreshTokenProtegido != null)
            .OrderBy(e => e.Nome)
            .ToListAsync(cancelamento);
        List<int> ids = empresas.Select(e => e.Id).ToList();
        List<CategoriaFinanceira> categorias = await _categorias.ConsultarSomenteLeitura()
            .Where(k => ids.Contains(k.EmpresaId) && k.Tipo != RegrasCategoria.TipoReceita)
            .OrderBy(k => k.EmpresaId)
            .ThenBy(k => k.BlingId)
            .ToListAsync(cancelamento);
        return new CategoriasDaRede(empresas, categorias);
    }

    private async Task<List<FornecedorDaRedeDto>> FornecedoresAsync(int clienteId, CancellationToken cancelamento)
    {
        var notas = await _notas.ConsultarSomenteLeitura()
            .Where(n => n.ClienteId == clienteId && n.CnpjFornecedor != "")
            .OrderByDescending(n => n.RecebidaEm)
            .Select(n => new { n.CnpjFornecedor, n.NomeFornecedor })
            .Take(5000)
            .ToListAsync(cancelamento);

        var porCnpj = new Dictionary<string, FornecedorDaRedeDto>();
        foreach (var nota in notas)
        {
            string cnpj = RegrasCategoria.ApenasDigitos(nota.CnpjFornecedor);
            if (cnpj == "" || porCnpj.ContainsKey(cnpj))
            {
                continue;
            }
            porCnpj[cnpj] = new FornecedorDaRedeDto { Cnpj = cnpj, Nome = nota.NomeFornecedor };
        }
        return porCnpj.Values.OrderBy(f => f.Nome).Take(MaximoFornecedores).ToList();
    }
}
