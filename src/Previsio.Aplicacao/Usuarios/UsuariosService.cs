using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Previsio.Core.Entidades;
using Previsio.Core.Abstracoes;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Mapeamentos;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Aplicacao.Usuarios;

/// <summary>
/// Cadastro de usuários. O admin (Soft.IO) cuida de todos; o gestor da rede,
/// só dos usuários da própria rede (Cliente ou Gestor).
/// </summary>
public class UsuariosService
{
    private const int TamanhoMaximoNome = 200;
    private const string MensagemNaoEncontrado = "Usuário não encontrado.";
    private const string MensagemEmailEmUso = "Já existe um usuário com esse e-mail.";

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

    public async Task<List<UsuarioDto>> ListarAsync(ClaimsPrincipal ator, int? clienteId, CancellationToken cancelamento)
    {
        // O gestor só vê a própria rede, seja qual for o filtro pedido
        if (!ator.EhAdmin())
        {
            clienteId = ator.ClienteId();
            if (!clienteId.HasValue)
            {
                return new List<UsuarioDto>();
            }
        }

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

    public async Task<Resultado<UsuarioDto>> CriarAsync(ClaimsPrincipal ator, NovoUsuarioDto dados, CancellationToken cancelamento)
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
            return Resultado<UsuarioDto>.Invalido(
                "Perfil deve ser " + Perfis.Cliente + ", " + Perfis.Gestor + " ou " + Perfis.Admin + ".");
        }

        int? clienteId = dados.ClienteId;
        if (!ator.EhAdmin())
        {
            if (perfil == PerfilUsuario.Admin)
            {
                return Resultado<UsuarioDto>.Invalido("O gestor cadastra só usuários da própria rede (Cliente ou Gestor).");
            }
            // O gestor sempre cadastra na própria rede
            clienteId = ator.ClienteId();
        }

        if (perfil == PerfilUsuario.Admin)
        {
            if (clienteId.HasValue)
            {
                return Resultado<UsuarioDto>.Invalido("Administrador não pertence a um cliente: deixe clienteId vazio.");
            }
        }
        else
        {
            if (!clienteId.HasValue)
            {
                return Resultado<UsuarioDto>.Invalido("Informe o clienteId do usuário.");
            }

            int idCliente = clienteId.Value;
            bool clienteExiste = await _clientes.ExisteAsync(c => c.Id == idCliente, cancelamento);
            if (!clienteExiste)
            {
                return Resultado<UsuarioDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
            }
        }

        bool emailEmUso = await _usuarios.ExisteAsync(u => u.Email == email, cancelamento);
        if (emailEmUso)
        {
            return Resultado<UsuarioDto>.Conflito(MensagemEmailEmUso);
        }

        var usuario = new Usuario();
        usuario.Nome = nome;
        usuario.Email = email;
        usuario.Perfil = perfil;
        usuario.ClienteId = clienteId;
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
            return Resultado<UsuarioDto>.Conflito(MensagemEmailEmUso);
        }

        return Resultado<UsuarioDto>.Ok(UsuarioMapeamento.ParaDto(usuario));
    }

    /// <summary>
    /// Altera nome, e-mail, perfil (Cliente ou Gestor), ativa/desativa ou redefine a senha.
    /// Desativar ou redefinir a senha derruba na hora as sessões abertas do usuário.
    /// O gestor só altera usuários da própria rede e não muda o próprio perfil nem se desativa,
    /// para a rede nunca ficar sem gestor.
    /// </summary>
    public async Task<Resultado<UsuarioDto>> AlterarAsync(
        ClaimsPrincipal ator, int usuarioId, AlteracaoUsuarioDto dados, CancellationToken cancelamento)
    {
        Usuario? usuario = await _usuarios.Consultar().FirstOrDefaultAsync(u => u.Id == usuarioId, cancelamento);
        if (usuario == null)
        {
            return Resultado<UsuarioDto>.NaoEncontrado(MensagemNaoEncontrado);
        }

        bool ehOProprio = ator.UsuarioId() == usuario.Id;
        if (!ator.EhAdmin())
        {
            // Usuário de outra rede (ou admin) responde como se não existisse
            if (usuario.Perfil == PerfilUsuario.Admin || usuario.ClienteId != ator.ClienteId())
            {
                return Resultado<UsuarioDto>.NaoEncontrado(MensagemNaoEncontrado);
            }
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

        if (dados.Email != null)
        {
            string email = RegrasUsuario.NormalizarEmail(dados.Email);
            if (!RegrasUsuario.EmailValido(email))
            {
                return Resultado<UsuarioDto>.Invalido("E-mail inválido.");
            }
            if (email != usuario.Email)
            {
                int id = usuario.Id;
                bool emailEmUso = await _usuarios.ExisteAsync(u => u.Email == email && u.Id != id, cancelamento);
                if (emailEmUso)
                {
                    return Resultado<UsuarioDto>.Conflito(MensagemEmailEmUso);
                }
                usuario.Email = email;
            }
        }

        if (dados.Perfil != null)
        {
            PerfilUsuario perfil;
            if (!Perfis.TentarLer(dados.Perfil, out perfil))
            {
                return Resultado<UsuarioDto>.Invalido("Perfil deve ser " + Perfis.Cliente + " ou " + Perfis.Gestor + ".");
            }
            if (perfil != usuario.Perfil)
            {
                if (perfil == PerfilUsuario.Admin || usuario.Perfil == PerfilUsuario.Admin)
                {
                    return Resultado<UsuarioDto>.Invalido("Só dá para trocar entre os perfis " + Perfis.Cliente + " e " + Perfis.Gestor + ".");
                }
                if (ehOProprio)
                {
                    return Resultado<UsuarioDto>.Invalido("Você não pode mudar o próprio perfil.");
                }
                usuario.Perfil = perfil;
                // O perfil vai no token: as sessões abertas precisam entrar de novo
                AutenticacaoService.RevogarTokens(usuario);
            }
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
            if (!dados.Ativo.Value && ehOProprio)
            {
                return Resultado<UsuarioDto>.Invalido("Você não pode desativar o próprio usuário.");
            }

            usuario.Ativo = dados.Ativo.Value;
            if (!usuario.Ativo)
            {
                AutenticacaoService.RevogarTokens(usuario);
            }
        }

        try
        {
            await _usuarios.SalvarAsync(cancelamento);
        }
        catch (DbUpdateException)
        {
            _usuarios.DescartarAlteracoes();
            return Resultado<UsuarioDto>.Conflito(MensagemEmailEmUso);
        }
        return Resultado<UsuarioDto>.Ok(UsuarioMapeamento.ParaDto(usuario));
    }
}
