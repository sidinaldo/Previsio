using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Previsio.Aplicacao;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.Certificados;
using Previsio.Aplicacao.Comum;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Sefaz;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Previsio.Infra;
using Previsio.SondagemSefaz;

// Sondagem da distribuição de NF-e da SEFAZ (por NSU): UMA consulta, só leitura, com o certificado A1
// de uma empresa do Previsio. Não registra ciência nem nenhum outro evento.
//
// Rodar da raiz do repositório:
//   dotnet run --project tools/Previsio.SondagemSefaz -- 3            (empresa de id 3, desde o NSU zero)
//   dotnet run --project tools/Previsio.SondagemSefaz -- 3 000000000001234   (a partir de um NSU)
//
// Cuidado: se a SEFAZ responder que não há nada novo, consultar de novo antes de 1 hora bloqueia o
// CNPJ por 1 hora (656 - consumo indevido). Rode uma vez e espere.

string pastaApi = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Previsio.Api"));
if (!Directory.Exists(pastaApi))
{
    Console.WriteLine("Rode a partir da raiz do repositório (não achei " + pastaApi + ").");
    return 1;
}
if (args.Length == 0)
{
    Console.WriteLine("Informe o id da empresa: dotnet run --project tools/Previsio.SondagemSefaz -- <empresaId> [ultNSU]");
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
servicos.AddPrevisioInfra(configuracao);
servicos.AddPrevisioAplicacao();
servicos.AddSingleton(new AmbienteAplicacao(true));
servicos.AddSingleton(TimeProvider.System);

using ServiceProvider provedor = servicos.BuildServiceProvider();
using IServiceScope escopo = provedor.CreateScope();
IRepositorio<Empresa> repositorio = escopo.ServiceProvider.GetRequiredService<IRepositorio<Empresa>>();
CertificadosService certificados = escopo.ServiceProvider.GetRequiredService<CertificadosService>();
ISefaz sefaz = escopo.ServiceProvider.GetRequiredService<ISefaz>();

int empresaId = int.Parse(args[0]);
string ultimoNsu = "0";
if (args.Length > 1)
{
    ultimoNsu = args[1];
}

Empresa? empresa = await repositorio.Consultar().FirstOrDefaultAsync(e => e.Id == empresaId);
if (empresa == null)
{
    Console.WriteLine("Empresa " + empresaId + " não encontrada.");
    return 1;
}

// O certificado da própria empresa ou o de outra empresa da rede com a mesma raiz de CNPJ (matriz)
Empresa? dono = null;
if (empresa.TemCertificado)
{
    dono = empresa;
}
else
{
    string raiz = empresa.Cnpj.Substring(0, Math.Min(8, empresa.Cnpj.Length));
    List<Empresa> daRede = await repositorio.Consultar().Where(e => e.ClienteId == empresa.ClienteId).ToListAsync();
    dono = daRede.FirstOrDefault(e => e.TemCertificado && e.Cnpj.StartsWith(raiz));
}
if (dono == null)
{
    Console.WriteLine("Nem a empresa nem outra da rede com a mesma raiz de CNPJ tem certificado A1 cadastrado.");
    return 1;
}
string? uf = empresa.Uf;
if (!UfIbge.Valida(uf))
{
    uf = dono.Uf;
}
if (!UfIbge.Valida(uf))
{
    Console.WriteLine("Cadastre a UF da empresa (ou da dona do certificado) em Empresas.");
    return 1;
}
X509Certificate2? certificado = certificados.Abrir(dono);
if (certificado == null)
{
    Console.WriteLine("O certificado de " + dono.Nome + " não abriu (senha, arquivo ou pasta de chaves diferente da API).");
    return 1;
}

Console.WriteLine("Empresa: " + empresa.Id + " - " + empresa.Nome + " (CNPJ " + empresa.Cnpj + ", UF " + uf + ")");
Console.WriteLine("Certificado: " + dono.Nome + ", válido até " + certificado.NotAfter.ToString("dd/MM/yyyy"));
Console.WriteLine("Consultando a distribuição a partir do NSU " + ultimoNsu + " (uma chamada, só leitura)...");

RetornoSefaz retorno;
try
{
    retorno = await sefaz.DistribuirPorNsuAsync(certificado, uf!, empresa.Cnpj, ultimoNsu, CancellationToken.None);
}
catch (Exception erro)
{
    Console.WriteLine("A consulta falhou: " + erro.Message);
    return 1;
}

var resumos = new List<ResumoDocumento>();
foreach (DocumentoDistribuido documento in retorno.Documentos)
{
    resumos.Add(ResumoDocumento.Ler(documento.Nsu, documento.Schema, documento.Xml));
}

var relatorio = new StringBuilder();
relatorio.AppendLine("# Sondagem da SEFAZ (distribuição por NSU)");
relatorio.AppendLine();
relatorio.AppendLine("Empresa " + empresa.Id + " (" + empresa.Nome + "), CNPJ " + empresa.Cnpj + ", UF " + uf + ", "
    + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + ". Certificado de " + dono.Nome + ".");
relatorio.AppendLine();
relatorio.AppendLine("| Campo | Valor |");
relatorio.AppendLine("|---|---|");
relatorio.AppendLine("| Status | " + retorno.Status + " - " + retorno.Motivo + " |");
relatorio.AppendLine("| NSU pedido | " + ultimoNsu + " |");
relatorio.AppendLine("| ultNSU devolvido | " + retorno.UltimoNsu + " |");
relatorio.AppendLine("| maxNSU (o mais novo que existe) | " + retorno.MaximoNsu + " |");
relatorio.AppendLine("| Documentos nesta chamada | " + resumos.Count + " |");
relatorio.AppendLine();
relatorio.AppendLine("## Por tipo");
relatorio.AppendLine();
relatorio.AppendLine("| Schema | Quantidade |");
relatorio.AppendLine("|---|---|");
foreach (IGrouping<string, ResumoDocumento> grupo in resumos.GroupBy(r => r.Schema).OrderBy(g => g.Key))
{
    relatorio.AppendLine("| " + grupo.Key + " | " + grupo.Count() + " |");
}
relatorio.AppendLine();
relatorio.AppendLine("## Documentos");
relatorio.AppendLine();
relatorio.AppendLine("| NSU | Schema | Chave | Emitente | Valor | Emissão | Detalhe |");
relatorio.AppendLine("|---|---|---|---|---|---|---|");
foreach (ResumoDocumento resumo in resumos)
{
    relatorio.AppendLine("| " + resumo.Nsu + " | " + resumo.Schema + " | " + resumo.Chave + " | " + resumo.Emitente + " | "
        + resumo.Valor + " | " + resumo.Emissao + " | " + resumo.Detalhe + " |");
}

string pastaSaida = Path.Combine(Directory.GetCurrentDirectory(), "tools", "Previsio.SondagemSefaz", "saida");
Directory.CreateDirectory(pastaSaida);
string arquivo = Path.Combine(pastaSaida, "sondagem-sefaz-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".md");
await File.WriteAllTextAsync(arquivo, relatorio.ToString());

Console.WriteLine("Status: " + retorno.Status + " - " + retorno.Motivo);
Console.WriteLine("ultNSU " + retorno.UltimoNsu + " / maxNSU " + retorno.MaximoNsu + " / " + resumos.Count + " documentos");
Console.WriteLine("Relatório: " + arquivo);
return 0;
