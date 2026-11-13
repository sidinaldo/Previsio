using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Previsio.Aplicacao;
using Previsio.Aplicacao.Bling;
using Previsio.Aplicacao.Comum;
using Previsio.Bling;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Previsio.Infra;
using Previsio.SondagemBling;

// Sondagem da API do Bling: usa o token de uma empresa já conectada no Previsio e grava
// um relatório (markdown) com o que cada recurso devolve.
//
// Rodar da raiz do repositório, com a API parada ou não (só lê):
//   dotnet run --project tools/Previsio.SondagemBling            (primeira empresa conectada)
//   dotnet run --project tools/Previsio.SondagemBling -- 3       (empresa de id 3)
//
// Usa o appsettings da API (conexão, chaves de criptografia e credenciais do Bling).

string pastaApi = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Previsio.Api"));
if (!Directory.Exists(pastaApi))
{
    Console.WriteLine("Rode a partir da raiz do repositório (não achei " + pastaApi + ").");
    return 1;
}

IConfiguration configuracao = new ConfigurationBuilder()
    .SetBasePath(pastaApi)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

string conexao = configuracao.GetConnectionString("Previsio")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Previsio no appsettings da API.");

// A pasta das chaves é relativa à pasta da API, como quando a API roda
string pastaChaves = configuracao["DataProtection:PastaChaves"] ?? "chaves-dataprotection";
if (!Path.IsPathRooted(pastaChaves))
{
    pastaChaves = Path.Combine(pastaApi, pastaChaves);
}

var servicos = new ServiceCollection();
servicos.AddLogging(log => log.AddConsole().SetMinimumLevel(LogLevel.Warning));
servicos.AddDbContext<PrevisioDbContext>(opcoes => opcoes.UseNpgsql(conexao));
servicos.AddScoped(typeof(IRepositorio<>), typeof(Repositorio<>));
servicos.AddDataProtection()
    .SetApplicationName("Previsio")
    .PersistKeysToFileSystem(new DirectoryInfo(pastaChaves));
servicos.Configure<BlingOptions>(configuracao.GetSection(BlingOptions.Secao));
servicos.AddSingleton<LimitadorRequisicoes>();
servicos.AddHttpClient<BlingClient>();
servicos.AddPrevisioInfra(configuracao);
servicos.AddPrevisioAplicacao();
servicos.AddSingleton(new AmbienteAplicacao(true));
servicos.AddSingleton(TimeProvider.System);

using ServiceProvider provedor = servicos.BuildServiceProvider();
using IServiceScope escopo = provedor.CreateScope();
IRepositorio<Empresa> empresas = escopo.ServiceProvider.GetRequiredService<IRepositorio<Empresa>>();
TokenBlingService tokens = escopo.ServiceProvider.GetRequiredService<TokenBlingService>();
BlingOptions opcoesBling = escopo.ServiceProvider.GetRequiredService<IOptions<BlingOptions>>().Value;

Empresa? empresa;
if (args.Length > 0)
{
    int empresaId = int.Parse(args[0]);
    empresa = await empresas.Consultar().FirstOrDefaultAsync(e => e.Id == empresaId);
}
else
{
    empresa = await empresas.Consultar()
        .Where(e => e.Ativo && e.BlingRefreshTokenProtegido != null)
        .OrderBy(e => e.Id)
        .FirstOrDefaultAsync();
}
if (empresa == null)
{
    Console.WriteLine("Nenhuma empresa conectada ao Bling encontrada.");
    return 1;
}

Console.WriteLine("Empresa: " + empresa.Id + " - " + empresa.Nome);
string token;
try
{
    token = await tokens.ObterAccessTokenAsync(empresa, CancellationToken.None);
}
catch (Exception erro)
{
    Console.WriteLine("Não consegui obter o token do Bling desta empresa: " + erro.Message);
    Console.WriteLine("Confira se a pasta de chaves (" + pastaChaves + ") é a mesma que a API usa e se a empresa está conectada.");
    return 1;
}

using var http = new HttpClient();
http.BaseAddress = new Uri(opcoesBling.ApiBaseUrl);
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

// Token recusado antes de vencer (o Bling revoga quando os escopos do aplicativo mudam):
// força a renovação uma vez. Se o refresh token também foi revogado, a empresa precisa reconectar.
HttpResponseMessage teste = await http.GetAsync("contas/pagar?pagina=1&limite=1");
if ((int)teste.StatusCode == 401)
{
    Console.WriteLine("Token recusado pelo Bling; renovando...");
    empresa.BlingTokenExpiraEm = DateTime.UtcNow.AddMinutes(-1);
    try
    {
        token = await tokens.ObterAccessTokenAsync(empresa, CancellationToken.None);
    }
    catch (Exception erro)
    {
        Console.WriteLine("A renovação também foi recusada: " + erro.Message);
        Console.WriteLine("Clique em Conectar Bling na empresa, no Previsio, e rode de novo.");
        return 1;
    }
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}

var relatorio = new StringBuilder();
relatorio.AppendLine("# Sondagem da API do Bling");
relatorio.AppendLine();
relatorio.AppendLine("Empresa " + empresa.Id + " (" + empresa.Nome + "), " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + ".");
relatorio.AppendLine();
relatorio.AppendLine("| Recurso | Status | Itens | O que queríamos saber |");
relatorio.AppendLine("|---|---|---|---|");
var detalhes = new StringBuilder();

async Task<RespostaBling> Consultar(Recurso recurso)
{
    // O Bling aceita 3 requisições por segundo
    await Task.Delay(400);
    Console.WriteLine("GET " + recurso.Url);
    HttpResponseMessage resposta = await http.GetAsync(recurso.Url);
    var resultado = new RespostaBling();
    resultado.Status = (int)resposta.StatusCode;
    resultado.Corpo = await resposta.Content.ReadAsStringAsync();
    try
    {
        using JsonDocument documento = JsonDocument.Parse(resultado.Corpo);
        JsonElement dados;
        if (documento.RootElement.TryGetProperty("data", out dados))
        {
            resultado.Dados = dados.Clone();
        }
    }
    catch (JsonException)
    {
        // Corpo que não é JSON vai como texto no relatório
    }

    relatorio.AppendLine("| " + recurso.Titulo + " | " + resultado.Status + " | " + resultado.Quantidade() + " | " + recurso.Pergunta + " |");

    detalhes.AppendLine("## " + recurso.Titulo);
    detalhes.AppendLine();
    detalhes.AppendLine("`GET " + recurso.Url + "` → " + resultado.Status);
    detalhes.AppendLine();
    if (resultado.Sucesso)
    {
        SortedSet<string> campos = resultado.Campos();
        if (campos.Count > 0)
        {
            detalhes.AppendLine("Campos: `" + string.Join("`, `", campos) + "`");
            detalhes.AppendLine();
        }
        JsonElement? primeiro = resultado.PrimeiroItem();
        if (primeiro.HasValue)
        {
            string exemplo = JsonSerializer.Serialize(primeiro.Value, new JsonSerializerOptions { WriteIndented = true });
            detalhes.AppendLine("```json");
            detalhes.AppendLine(Cortar(exemplo, 80));
            detalhes.AppendLine("```");
            detalhes.AppendLine();
        }
    }
    else
    {
        detalhes.AppendLine("```");
        detalhes.AppendLine(Cortar(resultado.Corpo, 20));
        detalhes.AppendLine("```");
        detalhes.AppendLine();
    }
    return resultado;
}

static string Cortar(string texto, int maximoLinhas)
{
    string[] linhas = texto.Split('\n');
    if (linhas.Length <= maximoLinhas)
    {
        return texto.TrimEnd();
    }
    return string.Join('\n', linhas.Take(maximoLinhas)).TrimEnd() + "\n... (" + (linhas.Length - maximoLinhas) + " linhas cortadas)";
}

// 1. Contas a receber: fluxo de caixa
RespostaBling receber = await Consultar(new Recurso("Contas a receber", "contas/receber?pagina=1&limite=20",
    "Dá para montar entradas × saídas por semana?"));
long? receberId = RespostaBling.LerId(receber.PrimeiroItem(), "id");
if (receberId.HasValue)
{
    await Consultar(new Recurso("Conta a receber (detalhe)", "contas/receber/" + receberId.Value,
        "Quais campos o detalhe traz (categoria, forma de pagamento, origem)?"));
}

// 2. Categorias de receitas e despesas
await Consultar(new Recurso("Categorias de receitas e despesas", "categorias/receitas-despesas?pagina=1&limite=100",
    "Quais categorias existem e se dá para usar no lançamento e no painel"));

// 3. Contas a pagar: detalhe de uma paga (categoria, forma de pagamento, borderô)
RespostaBling pagas = await Consultar(new Recurso("Contas a pagar pagas", "contas/pagar?pagina=1&limite=20&situacoes[]=2",
    "Existem contas pagas para ver como fica a baixa?"));
long? pagaId = RespostaBling.LerId(pagas.PrimeiroItem(), "id");
long? borderoId = null;
if (pagaId.HasValue)
{
    RespostaBling paga = await Consultar(new Recurso("Conta a pagar paga (detalhe)", "contas/pagar/" + pagaId.Value,
        "Traz categoria, forma de pagamento e o borderô (data real do pagamento)?"));
    borderoId = RespostaBling.LerId(paga.PrimeiroItem(), "borderos");
}
if (borderoId.HasValue)
{
    await Consultar(new Recurso("Borderô", "borderos/" + borderoId.Value,
        "Data do pagamento, valor pago, juros e desconto"));
}

// 4. Pedidos de compra: previsão antes da nota chegar
RespostaBling compras = await Consultar(new Recurso("Pedidos de compra", "pedidos/compras?pagina=1&limite=20",
    "Dá para prever a saída antes da nota chegar?"));
long? compraId = RespostaBling.LerId(compras.PrimeiroItem(), "id");
if (compraId.HasValue)
{
    await Consultar(new Recurso("Pedido de compra (detalhe)", "pedidos/compras/" + compraId.Value,
        "Traz parcelas e vencimentos? Liga com a nota de entrada?"));
}

// 5. Formas de pagamento
await Consultar(new Recurso("Formas de pagamento", "formas-pagamentos?pagina=1&limite=100",
    "Boleto, Pix, cartão: dá para lançar com a forma certa?"));

// 6. Contas financeiras (bancos e caixa): tem saldo?
RespostaBling contasFinanceiras = await Consultar(new Recurso("Contas contábeis / financeiras", "contas-contabeis?pagina=1&limite=100",
    "Existe saldo do banco para a previsão começar do saldo real?"));
long? contaFinanceiraId = RespostaBling.LerId(contasFinanceiras.PrimeiroItem(), "id");
if (contaFinanceiraId.HasValue)
{
    await Consultar(new Recurso("Conta contábil (detalhe)", "contas-contabeis/" + contaFinanceiraId.Value,
        "O detalhe traz saldo?"));
}

// 7. Movimentações de caixa e bancos
await Consultar(new Recurso("Caixas e bancos (movimentações)", "caixas?pagina=1&limite=20",
    "Existe extrato/movimentação para conciliar pagamentos?"));

// 8. Notificações
await Consultar(new Recurso("Notificações", "notificacoes?pagina=1&limite=20",
    "O Bling avisa mudanças (webhooks) ou só lista notificações do usuário?"));

relatorio.AppendLine();
relatorio.AppendLine("Webhooks não aparecem na API: conferir na Área do integrador, no cadastro do aplicativo, quais eventos podem ser assinados.");
relatorio.AppendLine();
relatorio.Append(detalhes);

string pastaSaida = Path.Combine(Directory.GetCurrentDirectory(), "tools", "Previsio.SondagemBling", "saida");
Directory.CreateDirectory(pastaSaida);
string arquivo = Path.Combine(pastaSaida, "sondagem-bling-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".md");
await File.WriteAllTextAsync(arquivo, relatorio.ToString());
Console.WriteLine("Relatório: " + arquivo);
return 0;
