using Microsoft.EntityFrameworkCore;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Mapeamentos;
using Previsio.Core.Entidades;
using Previsio.Data.Repositorios;
using Previsio.Core.Abstracoes;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Servicos;

/// <summary>
/// Cadastro de usuários (administradores e usuários dos clientes).
/// </summary>
public class UsuariosService
{
    private const int TamanhoMaximoNome = 200;

    private readonly IRepositorio<Usuario> _usuarios;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly AutenticacaoService _autenticacao;

    public UsuariosService(
        IRepositorio<Usuario> usuarios,
        IRepositorio<Cliente> clientes,
        AutenticacaoService autenticacao)
    {
        _usuarios = usuarios;
        _clientes = clientes;
        _autenticacao = autenticacao;
    }

    public async Task<List<UsuarioDto>> ListarAsync(int? clienteId, CancellationToken cancelamento)
    {
        IQueryable<Usuario> consulta = _usuarios.ConsultarSomenteLeitura();
        if (clienteId.HasValue)
        {
            consulta = consulta.Where(u => u.ClienteId == clienteId.Value);
        }

        List<Usuario> usuarios = await consulta
            .OrderBy(u => u.Nome)
            .ToListAsync(cancelamento);

        var lista = new List<UsuarioDto>();
        foreach (Usuario usuario in usuarios)
        {
            lista.Add(UsuarioMapeamento.ParaDto(usuario));
        }
        return lista;
    }

    public async Task<Resultado<UsuarioDto>> CriarAsync(NovoUsuarioDto dados, CancellationToken cancelamento)
    {
        string nome = dados.Nome.Trim();
        if (nome == "" || nome.Length > TamanhoMaximoNome)
        {
            return Resultado<UsuarioDto>.Invalido("Informe o nome do usuário (até " + TamanhoMaximoNome + " caracteres).");
        }

        string email = RegrasUsuario.NormalizarEmail(dados.Email);
        if (!RegrasUsuario.EmailValido(email))
        {
            return Resultado<UsuarioDto>.Invalido("E-mail inválido.");
        }

        string? erroSenha = RegrasUsuario.ValidarSenha(dados.Senha);
        if (erroSenha != null)
        {
            return Resultado<UsuarioDto>.Invalido(erroSenha);
        }

        PerfilUsuario perfil;
        if (!Perfis.TentarLer(dados.Perfil, out perfil))
        {
            return Resultado<UsuarioDto>.Invalido("Perfil deve ser " + Perfis.Cliente + " ou " + Perfis.Admin + ".");
        }

        if (perfil == PerfilUsuario.Admin)
        {
            if (dados.ClienteId.HasValue)
            {
                return Resultado<UsuarioDto>.Invalido("Administrador não pertence a um cliente: deixe clienteId vazio.");
            }
        }
        else
        {
            if (!dados.ClienteId.HasValue)
            {
                return Resultado<UsuarioDto>.Invalido("Informe o clienteId do usuário.");
            }

            bool clienteExiste = await _clientes.ExisteAsync(c => c.Id == dados.ClienteId.Value, cancelamento);
            if (!clienteExiste)
            {
                return Resultado<UsuarioDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
            }
        }

        bool emailEmUso = await _usuarios.ExisteAsync(u => u.Email == email, cancelamento);
        if (emailEmUso)
        {
            return Resultado<UsuarioDto>.Conflito("Já existe um usuário com esse e-mail.");
        }

        var usuario = new Usuario();
        usuario.Nome = nome;
        usuario.Email = email;
        usuario.Perfil = perfil;
        usuario.ClienteId = dados.ClienteId;
        _autenticacao.DefinirSenha(usuario, dados.Senha);

        _usuarios.Adicionar(usuario);

        try
        {
            await _usuarios.SalvarAsync(cancelamento);
        }
        catch (DbUpdateException)
        {
            // Outro cadastro gravou o mesmo e-mail ao mesmo tempo (índice único)
            _usuarios.DescartarAlteracoes();
            return Resultado<UsuarioDto>.Conflito("Já existe um usuário com esse e-mail.");
        }

        return Resultado<UsuarioDto>.Ok(UsuarioMapeamento.ParaDto(usuario));
    }

    /// <summary>
    /// Altera nome, ativa/desativa ou redefine a senha. Desativar ou redefinir a senha
    /// derruba na hora as sessões abertas do usuário.
    /// </summary>
    public async Task<Resultado<UsuarioDto>> AlterarAsync(
        int usuarioId, AlteracaoUsuarioDto dados, int? usuarioLogadoId, CancellationToken cancelamento)
    {
        Usuario? usuario = await _usuarios.Consultar().FirstOrDefaultAsync(u => u.Id == usuarioId, cancelamento);
        if (usuario == null)
        {
            return Resultado<UsuarioDto>.NaoEncontrado("Usuário não encontrado.");
        }

        if (dados.Nome != null)
        {
            string nome = dados.Nome.Trim();
            if (nome == "" || nome.Length > TamanhoMaximoNome)
            {
                return Resultado<UsuarioDto>.Invalido("Informe o nome do usuário (até " + TamanhoMaximoNome + " caracteres).");
            }
            usuario.Nome = nome;
        }

        if (dados.NovaSenha != null)
        {
            string? erroSenha = RegrasUsuario.ValidarSenha(dados.NovaSenha);
            if (erroSenha != null)
            {
                return Resultado<UsuarioDto>.Invalido(erroSenha);
            }
            _autenticacao.DefinirSenha(usuario, dados.NovaSenha);
        }

        if (dados.Ativo.HasValue && dados.Ativo.Value != usuario.Ativo)
        {
            if (!dados.Ativo.Value && usuarioLogadoId == usuario.Id)
            {
                return Resultado<UsuarioDto>.Invalido("Você não pode desativar o próprio usuário.");
            }

            usuario.Ativo = dados.Ativo.Value;
            if (!usuario.Ativo)
            {
                AutenticacaoService.RevogarTokens(usuario);
            }
        }

        await _usuarios.SalvarAsync(cancelamento);
        return Resultado<UsuarioDto>.Ok(UsuarioMapeamento.ParaDto(usuario));
    }
}
