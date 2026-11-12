using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Infra.Email;
using Previsio.Infra.Sefaz;
using Previsio.Infra.Jobs;
using Previsio.Infra.Seguranca;

namespace Previsio.Infra;

/// <summary>
/// Registra na injeção de dependência as implementações técnicas das interfaces da Aplicacao.
/// O Data Protection e o Hangfire em si (armazenamento e servidor) continuam configurados no Program.
/// </summary>
public static class RegistroInfra
{
    public static IServiceCollection AddPrevisioInfra(this IServiceCollection servicos, IConfiguration configuracao)
    {
        // Criptografia dos segredos gravados no banco
        servicos.AddSingleton<IProtetorSegredos, ProtetorSegredos>();
        servicos.AddSingleton<IProtetorTemporario, ProtetorTemporario>();
        servicos.AddSingleton<IHashSenha, HashSenhaIdentity>();

        // E-mail: leitura e teste de conexão IMAP, envio SMTP
        servicos.AddScoped<IConexaoImap, ConexaoImapMailKit>();
        servicos.AddSingleton<ILeitorImap, LeitorImapMailKit>();
        servicos.Configure<SmtpOptions>(configuracao.GetSection(SmtpOptions.Secao));
        servicos.AddSingleton<IEnvioEmail, EnvioEmailSmtp>();

        // SEFAZ: consulta por chave e ciência da operação
        servicos.Configure<SefazOptions>(configuracao.GetSection(SefazOptions.Secao));
        servicos.AddScoped<ISefaz, SefazSoap>();

        // Hangfire: o que está agendado e o que está rodando (tela Saúde)
        servicos.AddSingleton<IExecucaoJobs, ExecucaoJobsHangfire>();
        servicos.AddSingleton<IAgendaJobs, AgendaJobsHangfire>();

        return servicos;
    }
}
