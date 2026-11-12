using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Jobs;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Bling;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Infra.Seguranca;

namespace Previsio.Api.Tests.Jobs;

public class LancamentoNotaJobTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());

    private LancamentoNotaJob CriarJob(RespostasFixas respostas)
    {
        BlingClient bling = BlingDeTeste.Criar(respostas);
        var tokens = new TokenBlingService(bling, _db, _protetor, NullLogger<TokenBlingService>.Instance);
        return new LancamentoNotaJob(_db, bling, tokens, NullLogger<LancamentoNotaJob>.Instance);
    }

    private async Task<NotaFiscal> CadastrarNota()
    {
        var cliente = new Cliente();
        cliente.Nome = "Rede Alfa";
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();

        var empresa = new Empresa();
        empresa.ClienteId = cliente.Id;
        empresa.Nome = "Filial Centro";
        empresa.Cnpj = "98765432000110";
        empresa.BlingAccessTokenProtegido = _protetor.Proteger("access-valido");
        empresa.BlingRefreshTokenProtegido = _protetor.Proteger("refresh-valido");
        empresa.BlingTokenExpiraEm = DateTime.UtcNow.AddHours(2);
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();

        var nota = new NotaFiscal();
        nota.ClienteId = cliente.Id;
        nota.EmpresaId = empresa.Id;
        nota.ChaveAcesso = "24251112345678000199550010000482131000482130";
        nota.Numero = "48213";
        nota.NomeFornecedor = "ROTA SUL PECAS LTDA";
        nota.CnpjFornecedor = "12345678000199";
        nota.CnpjDestinatario = empresa.Cnpj;
        nota.ValorTotal = 6480m;
        nota.DataEmissao = new DateTime(2026, 11, 6);
        nota.OrigemEmail = "fornecedor@rotasul.com.br | NF 48213";

        var parcela = new ParcelaNota();
        parcela.NumeroParcela = "001";
        parcela.Valor = 3240m;
        parcela.Vencimento = new DateTime(2026, 11, 12);
        nota.Parcelas.Add(parcela);

        _db.NotasFiscais.Add(nota);
        await _db.SaveChangesAsync();
        return nota;
    }

    [Fact]
    public async Task ContaAPagarRecusada_RegistraFalhaComNotaEmpresaValorEEndereco()
    {
        NotaFiscal nota = await CadastrarNota();
        var respostas = new RespostasFixas(requisicao =>
        {
            if (requisicao.Method == HttpMethod.Get)
            {
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":[{\"id\":555,\"nome\":\"ROTA SUL\"}]}");
            }
            return RespostasFixas.Json(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Vencimento inválido\"}}");
        });

        bool blingRespondeu = await CriarJob(respostas).LancarAsync(nota.Id, CancellationToken.None);

        Assert.True(blingRespondeu);
        FalhaProcessamento falha = Assert.Single(_db.Falhas.ToList());
        Assert.Equal(EtapaFalha.LancamentoContaPagar, falha.Etapa);
        Assert.Equal("POST", falha.Metodo);
        Assert.Equal(BlingDeTeste.ApiBaseUrl + "contas/pagar", falha.Endereco);
        Assert.Equal(400, falha.StatusHttp);
        Assert.Contains("Vencimento inválido", falha.Resposta);

        Assert.Equal(nota.Id, falha.NotaFiscalId);
        Assert.Equal(nota.EmpresaId, falha.EmpresaId);
        Assert.Equal(nota.ClienteId, falha.ClienteId);
        Assert.Equal("48213", falha.NumeroNota);
        Assert.Equal("ROTA SUL PECAS LTDA", falha.Fornecedor);
        Assert.Equal(6480m, falha.ValorNota);
        Assert.Equal("001", falha.NumeroParcela);
        Assert.Equal(3240m, falha.ValorParcela);

        Assert.Equal(StatusNota.Erro, nota.Status);
        Assert.Equal(1, nota.Tentativas);
    }

    [Fact]
    public async Task SemConexaoNaBuscaDoFornecedor_RegistraFalhaSemGastarTentativa()
    {
        NotaFiscal nota = await CadastrarNota();
        var respostas = new RespostasFixas(_ => throw new HttpRequestException("Tempo de conexão esgotado"));

        bool blingRespondeu = await CriarJob(respostas).LancarAsync(nota.Id, CancellationToken.None);

        Assert.False(blingRespondeu);
        FalhaProcessamento falha = Assert.Single(_db.Falhas.ToList());
        Assert.Equal(EtapaFalha.BuscaFornecedor, falha.Etapa);
        Assert.Equal("GET", falha.Metodo);
        Assert.StartsWith(BlingDeTeste.ApiBaseUrl + "contatos", falha.Endereco);
        Assert.Null(falha.StatusHttp);

        Assert.Equal(StatusNota.Erro, nota.Status);
        Assert.Equal(0, nota.Tentativas);
    }

    [Fact]
    public async Task RenovacaoDoTokenRecusada_RegistraFalhaEDesconectaAEmpresa()
    {
        NotaFiscal nota = await CadastrarNota();
        Empresa empresa = _db.Empresas.Single();
        empresa.BlingTokenExpiraEm = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        var respostas = new RespostasFixas(_ => RespostasFixas.Json(
            HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\"}"));

        bool blingRespondeu = await CriarJob(respostas).LancarAsync(nota.Id, CancellationToken.None);

        Assert.True(blingRespondeu);
        FalhaProcessamento falha = Assert.Single(_db.Falhas.ToList());
        Assert.Equal(EtapaFalha.RenovacaoToken, falha.Etapa);
        Assert.Equal(new BlingOptions().TokenUrl, falha.Endereco);
        Assert.Equal("48213", falha.NumeroNota);

        Assert.Equal(StatusNota.BlingDesconectado, nota.Status);
        Assert.False(empresa.BlingConectado);
    }

    [Fact]
    public async Task SemConexaoNaRenovacao_NaoDesconectaAEmpresa()
    {
        NotaFiscal nota = await CadastrarNota();
        Empresa empresa = _db.Empresas.Single();
        empresa.BlingTokenExpiraEm = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        var respostas = new RespostasFixas(_ => throw new HttpRequestException("Sem rede"));

        bool blingRespondeu = await CriarJob(respostas).LancarAsync(nota.Id, CancellationToken.None);

        Assert.False(blingRespondeu);
        Assert.True(empresa.BlingConectado);
        FalhaProcessamento falha = Assert.Single(_db.Falhas.ToList());
        Assert.Equal(EtapaFalha.RenovacaoToken, falha.Etapa);
    }
}
