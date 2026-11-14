using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Pagamento;

namespace Previsio.Aplicacao.FormasPagamento;

/// <summary>
/// Configuração das formas de pagamento da rede: forma padrão e regras "fornecedor → forma".
/// As formas são escolhidas pelo nome; a tela avisa em quais empresas o nome não existe no Bling.
/// </summary>
public class FormasPagamentoService
{
    public const string MensagemRegraNaoEncontrada = "Regra não encontrada.";

    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<FormaPagamento> _formas;
    private readonly IRepositorio<RegraFormaPagamento> _regras;
    private readonly IRepositorio<NotaFiscal> _notas;
    private readonly FormasPagamentoOptions _opcoes;

    public FormasPagamentoService(
        IRepositorio<Cliente> clientes,
        IRepositorio<Empresa> empresas,
        IRepositorio<FormaPagamento> formas,
        IRepositorio<RegraFormaPagamento> regras,
        IRepositorio<NotaFiscal> notas,
        IOptions<FormasPagamentoOptions> opcoes)
    {
        _clientes = clientes;
        _empresas = empresas;
        _formas = formas;
        _regras = regras;
        _notas = notas;
        _opcoes = opcoes.Value;
    }

    public async Task<Resultado<ConfiguracaoFormasPagamentoDto>> ObterAsync(int clienteId, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.ConsultarSomenteLeitura().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ConfiguracaoFormasPagamentoDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }

        FormasPagamentoDaRede rede = await CarregarRedeAsync(clienteId, cancelamento);
        List<string> saiSozinho = _opcoes.GruposSaiSozinhoValidos();
        var dto = new ConfiguracaoFormasPagamentoDto();
        dto.FormaPadrao = cliente.FormaPagamentoPadrao;
        dto.PadraoAutomatico = _opcoes.PadraoAutomaticoValido();
        dto.EmpresasSemFormas = rede.EmpresasSemFormas();
        foreach (string grupo in saiSozinho)
        {
            dto.GruposSaiSozinho.Add(GruposFormaPagamento.Nome(grupo));
        }
        foreach (FormaPagamento forma in rede.Opcoes())
        {
            var opcao = new OpcaoFormaPagamentoDto();
            opcao.Nome = forma.Descricao.Trim();
            opcao.Grupo = forma.Grupo;
            opcao.GrupoNome = GruposFormaPagamento.Nome(forma.Grupo);
            opcao.SaiSozinho = saiSozinho.Contains(forma.Grupo);
            dto.Opcoes.Add(opcao);
        }

        // A que vale de fato: a escolhida pela rede ou, sem ela, a automática
        string? padraoEmUso = cliente.FormaPagamentoPadrao;
        if (string.IsNullOrWhiteSpace(padraoEmUso))
        {
            padraoEmUso = dto.PadraoAutomatico;
        }
        if (padraoEmUso != null)
        {
            dto.PadraoAusenteEm = rede.AusenteEm(padraoEmUso);
        }

        List<RegraFormaPagamento> regras = await _regras.ConsultarSomenteLeitura()
            .Where(r => r.ClienteId == clienteId)
            .ToListAsync(cancelamento);
        List<FornecedorDaRedeDto> fornecedores = await FornecedoresDaRede.ListarAsync(
            _notas.ConsultarSomenteLeitura(), clienteId, cancelamento);
        var nomesFornecedores = new Dictionary<string, string>();
        foreach (FornecedorDaRedeDto fornecedor in fornecedores)
        {
            nomesFornecedores[fornecedor.Cnpj] = fornecedor.Nome;
        }

        foreach (RegraFormaPagamento regra in regras)
        {
            var item = new RegraFormaPagamentoDto();
            item.Id = regra.Id;
            item.CnpjFornecedor = regra.CnpjFornecedor;
            string? nome;
            if (nomesFornecedores.TryGetValue(regra.CnpjFornecedor, out nome))
            {
                item.FornecedorNome = nome;
            }
            item.Forma = regra.Forma;
            item.AusenteEm = rede.AusenteEm(regra.Forma);
            dto.Regras.Add(item);
        }
        dto.Regras = dto.Regras.OrderBy(r => r.FornecedorNome ?? r.CnpjFornecedor).ToList();
        dto.Fornecedores = fornecedores;
        return Resultado<ConfiguracaoFormasPagamentoDto>.Ok(dto);
    }

    public async Task<Resultado<ConfiguracaoFormasPagamentoDto>> AlterarPadraoAsync(
        int clienteId, FormaPagamentoPadraoDto dados, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ConfiguracaoFormasPagamentoDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }

        string forma = (dados.Forma ?? "").Trim();
        if (forma == "")
        {
            cliente.FormaPagamentoPadrao = null;
        }
        else
        {
            FormasPagamentoDaRede rede = await CarregarRedeAsync(clienteId, cancelamento);
            string? nome = rede.NomeExistente(forma);
            if (nome == null)
            {
                return Resultado<ConfiguracaoFormasPagamentoDto>.Invalido(MensagemFormaInexistente(forma));
            }
            cliente.FormaPagamentoPadrao = nome;
        }
        await _clientes.SalvarAsync(cancelamento);
        return await ObterAsync(clienteId, cancelamento);
    }

    public async Task<Resultado<ConfiguracaoFormasPagamentoDto>> SalvarRegraAsync(
        int clienteId, NovaRegraFormaPagamentoDto dados, CancellationToken cancelamento)
    {
        bool existe = await _clientes.ExisteAsync(c => c.Id == clienteId, cancelamento);
        if (!existe)
        {
            return Resultado<ConfiguracaoFormasPagamentoDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }

        string cnpj = FornecedoresDaRede.ApenasDigitos(dados.CnpjFornecedor);
        if (cnpj.Length != 11 && cnpj.Length != 14)
        {
            return Resultado<ConfiguracaoFormasPagamentoDto>.Invalido("Informe o CNPJ (ou CPF) do fornecedor.");
        }
        string forma = (dados.Forma ?? "").Trim();
        if (forma == "")
        {
            return Resultado<ConfiguracaoFormasPagamentoDto>.Invalido("Escolha a forma de pagamento.");
        }
        FormasPagamentoDaRede rede = await CarregarRedeAsync(clienteId, cancelamento);
        string? nome = rede.NomeExistente(forma);
        if (nome == null)
        {
            return Resultado<ConfiguracaoFormasPagamentoDto>.Invalido(MensagemFormaInexistente(forma));
        }

        // Uma regra por fornecedor: se já existe, troca a forma
        RegraFormaPagamento? regra = await _regras.Consultar()
            .FirstOrDefaultAsync(r => r.ClienteId == clienteId && r.CnpjFornecedor == cnpj, cancelamento);
        if (regra == null)
        {
            regra = new RegraFormaPagamento();
            regra.ClienteId = clienteId;
            regra.CnpjFornecedor = cnpj;
            _regras.Adicionar(regra);
        }
        regra.Forma = nome;
        await _regras.SalvarAsync(cancelamento);
        return await ObterAsync(clienteId, cancelamento);
    }

    public async Task<Resultado<ConfiguracaoFormasPagamentoDto>> RemoverRegraAsync(
        int clienteId, int regraId, CancellationToken cancelamento)
    {
        RegraFormaPagamento? regra = await _regras.Consultar()
            .FirstOrDefaultAsync(r => r.Id == regraId && r.ClienteId == clienteId, cancelamento);
        if (regra == null)
        {
            return Resultado<ConfiguracaoFormasPagamentoDto>.NaoEncontrado(MensagemRegraNaoEncontrada);
        }
        _regras.Remover(regra);
        await _regras.SalvarAsync(cancelamento);
        return await ObterAsync(clienteId, cancelamento);
    }

    private static string MensagemFormaInexistente(string forma)
    {
        return "A forma de pagamento \"" + forma + "\" não existe (ativa) no Bling de nenhuma empresa da rede.";
    }

    private async Task<FormasPagamentoDaRede> CarregarRedeAsync(int clienteId, CancellationToken cancelamento)
    {
        List<Empresa> empresas = await _empresas.ConsultarSomenteLeitura()
            .Where(e => e.ClienteId == clienteId && e.Ativo && e.BlingRefreshTokenProtegido != null)
            .OrderBy(e => e.Nome)
            .ToListAsync(cancelamento);
        List<int> ids = empresas.Select(e => e.Id).ToList();
        List<FormaPagamento> formas = await _formas.ConsultarSomenteLeitura()
            .Where(f => ids.Contains(f.EmpresaId) && f.Situacao == 1)
            .OrderBy(f => f.EmpresaId)
            .ThenBy(f => f.BlingId)
            .ToListAsync(cancelamento);
        return new FormasPagamentoDaRede(empresas, formas);
    }
}
