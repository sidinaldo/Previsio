using Microsoft.Extensions.DependencyInjection;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Avisos;
using Previsio.Aplicacao.Bling;
using Previsio.Aplicacao.Certificados;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Conferencia;
using Previsio.Aplicacao.Contas;
using Previsio.Aplicacao.Contratos;
using Previsio.Aplicacao.Email;
using Previsio.Aplicacao.EmailsIgnorados;
using Previsio.Aplicacao.Empresas;
using Previsio.Aplicacao.Exportacao;
using Previsio.Aplicacao.Falhas;
using Previsio.Aplicacao.LeituraEmail;
using Previsio.Aplicacao.Notas;
using Previsio.Aplicacao.NotasSoPdf;
using Previsio.Aplicacao.Painel;
using Previsio.Aplicacao.Resumos;
using Previsio.Aplicacao.Saude;
using Previsio.Aplicacao.Sincronizacao;
using Previsio.Aplicacao.Usuarios;
using Previsio.Aplicacao.Vencimentos;
using Previsio.Core.NFe;
using Previsio.Core.Painel;

namespace Previsio.Aplicacao;

/// <summary>
/// Registra os serviços de aplicação na injeção de dependência. As interfaces de Abstracoes são
/// registradas pela Infra (AddPrevisioInfra) e pela API (fila de jobs); os repositórios, pelo Program.
/// </summary>
public static class RegistroAplicacao
{
    public static IServiceCollection AddPrevisioAplicacao(this IServiceCollection servicos)
    {
        // Leitura da NF-e e cálculo do painel (Core)
        servicos.AddSingleton<LeitorNFe>();
        servicos.AddSingleton<CalculadoraPainel>();

        // Clientes, empresas, usuários e acesso
        servicos.AddScoped<ClientesService>();
        servicos.AddScoped<EmpresasService>();
        servicos.AddScoped<AcessoEmpresaService>();
        servicos.AddScoped<UsuariosService>();
        servicos.AddScoped<AutenticacaoService>();
        servicos.AddScoped<RedefinicaoSenhaService>();

        // Bling
        servicos.AddScoped<TokenBlingService>();
        servicos.AddScoped<BlingConexaoService>();
        servicos.AddScoped<ConferenciaBlingService>();
        servicos.AddScoped<ConfiguracaoConferenciaService>();
        servicos.AddScoped<FornecedoresBlingService>();
        servicos.AddScoped<ContasAntigasEmAberto>();

        // Leitura das caixas de e-mail
        servicos.AddScoped<LeituraEmailService>();

        // Notas e falhas
        servicos.AddScoped<RegistroNotaService>();
        servicos.AddScoped<NotasService>();
        servicos.AddScoped<FalhasService>();

        // Notas só em PDF e certificado A1
        servicos.AddScoped<RegistroNotaSoPdf>();
        servicos.AddScoped<NotasSoPdfService>();
        servicos.AddScoped<BuscaXmlSefazService>();
        servicos.AddScoped<CertificadosService>();
        servicos.AddScoped<EmailsIgnoradosService>();

        // Painel, contas a pagar e exportação
        servicos.AddScoped<PainelService>();
        servicos.AddScoped<ContasPagarService>();
        servicos.AddScoped<ExportacaoService>();

        // Avisos, e-mails e contrato
        servicos.AddScoped<AvisosService>();
        servicos.AddScoped<ResumoSemanalService>();
        servicos.AddScoped<AlertaVencimentosService>();
        servicos.AddScoped<TesteEmailService>();
        servicos.AddScoped<ContratosService>();
        servicos.AddScoped<AvisosTesteService>();
        servicos.AddScoped<SaudeService>();

        return servicos;
    }
}
