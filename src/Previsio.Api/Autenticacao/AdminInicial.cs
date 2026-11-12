using Previsio.Core.Entidades;
using Microsoft.EntityFrameworkCore;
using Previsio.Core.Abstracoes;
using Previsio.Aplicacao.Autenticacao;

namespace Previsio.Api.Autenticacao;

/// <summary>
/// Cria o primeiro administrador a partir da seção "AdminInicial" do appsettings,
/// só quando ainda não existe nenhum administrador. Depois do primeiro acesso,
/// apague a senha da configuração e troque-a em POST /api/auth/trocar-senha.
/// </summary>
public static class AdminInicial
{
    public const string Secao = "AdminInicial";

    public static async Task GarantirAsync(
        IRepositorio<Usuario> usuarios,
        AutenticacaoService autenticacao,
        IConfiguration configuracao,
        ILogger logger,
        CancellationToken cancelamento)
    {
        string email = RegrasUsuario.NormalizarEmail(configuracao[Secao + ":Email"]);
        string senha = configuracao[Secao + ":Senha"] ?? "";
        string nome = (configuracao[Secao + ":Nome"] ?? "").Trim();

        if (email == "" || senha == "")
        {
            return;
        }

        bool existeAdmin = await usuarios.ConsultarSomenteLeitura().AnyAsync(u => u.Perfil == PerfilUsuario.Admin, cancelamento);
        if (existeAdmin)
        {
            return;
        }

        if (!RegrasUsuario.EmailValido(email))
        {
            throw new InvalidOperationException("AdminInicial:Email não é um e-mail válido.");
        }

        string? erroSenha = RegrasUsuario.ValidarSenha(senha);
        if (erroSenha != null)
        {
            throw new InvalidOperationException("AdminInicial:Senha inválida. " + erroSenha);
        }

        bool emailEmUso = await usuarios.ConsultarSomenteLeitura().AnyAsync(u => u.Email == email, cancelamento);
        if (emailEmUso)
        {
            throw new InvalidOperationException("AdminInicial:Email já pertence a um usuário que não é administrador.");
        }

        var admin = new Usuario();
        admin.Nome = nome;
        if (admin.Nome == "")
        {
            admin.Nome = "Administrador";
        }
        admin.Email = email;
        admin.Perfil = PerfilUsuario.Admin;
        autenticacao.DefinirSenha(admin, senha);

        usuarios.Adicionar(admin);
        await usuarios.SalvarAsync(cancelamento);

        logger.LogWarning("Administrador inicial {Email} criado. Remova AdminInicial:Senha da configuração.", email);
    }
}
