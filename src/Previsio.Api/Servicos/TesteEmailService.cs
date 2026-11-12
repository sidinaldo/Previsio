using System.Net.Sockets;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Dtos;
using Previsio.Core.Entidades;
using Previsio.Data.Repositorios;
using Previsio.Core.Abstracoes;

namespace Previsio.Api.Servicos;

/// <summary>
/// Confere se o Previsio consegue entrar na caixa de e-mail antes de gravar o cadastro,
/// para um erro de senha aparecer na hora e não só em Falhas depois do job.
/// </summary>
public class TesteEmailService
{
    public const int SegundosLimite = 15;

    private readonly IConexaoImap _conexao;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly ProtetorSegredos _protetor;

    public TesteEmailService(IConexaoImap conexao, IRepositorio<Cliente> clientes, ProtetorSegredos protetor)
    {
        _conexao = conexao;
        _clientes = clientes;
        _protetor = protetor;
    }

    public async Task<Resultado<ResultadoTesteEmailDto>> TestarAsync(TesteEmailDto dados, CancellationToken cancelamento)
    {
        string host = (dados.ImapHost ?? "").Trim();
        string usuario = (dados.ImapUsuario ?? "").Trim();
        if (host == "" || usuario == "")
        {
            return Resultado<ResultadoTesteEmailDto>.Invalido("Informe o servidor IMAP e o usuário da caixa de e-mail.");
        }
        if (dados.ImapPorta <= 0 || dados.ImapPorta > 65535)
        {
            return Resultado<ResultadoTesteEmailDto>.Invalido("Informe uma porta entre 1 e 65535.");
        }

        string senha = dados.ImapSenha ?? "";
        if (senha == "")
        {
            if (dados.ClienteId == null)
            {
                return Resultado<ResultadoTesteEmailDto>.Invalido("Informe a senha da caixa de e-mail.");
            }

            Cliente? cliente = await _clientes.ConsultarSomenteLeitura()
                .FirstOrDefaultAsync(c => c.Id == dados.ClienteId.Value, cancelamento);
            if (cliente == null)
            {
                return Resultado<ResultadoTesteEmailDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
            }
            if (string.IsNullOrEmpty(cliente.ImapSenhaProtegida))
            {
                return Resultado<ResultadoTesteEmailDto>.Invalido("Informe a senha da caixa de e-mail.");
            }
            senha = _protetor.Revelar(cliente.ImapSenhaProtegida);
        }

        var resposta = new ResultadoTesteEmailDto();
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        limite.CancelAfter(TimeSpan.FromSeconds(SegundosLimite));
        try
        {
            await _conexao.TestarAsync(host, dados.ImapPorta, dados.ImapSsl, usuario, senha, limite.Token);
            resposta.Conectou = true;
            resposta.Mensagem = "Conectado. O Previsio conseguiu entrar na caixa " + usuario + ".";
        }
        catch (OperationCanceledException)
        {
            // Quem chamou desistiu (a requisição caiu): não é falha do servidor de e-mail
            if (cancelamento.IsCancellationRequested)
            {
                throw;
            }
            resposta.Mensagem = "O servidor " + host + " não respondeu em " + SegundosLimite + " segundos. Confira o servidor e a porta.";
        }
        catch (AuthenticationException)
        {
            resposta.Mensagem = "O servidor recusou o usuário ou a senha. No Gmail e no Outlook, use uma senha de app, não a senha normal da conta.";
        }
        catch (SslHandshakeException)
        {
            resposta.Mensagem = "Falhou a conexão segura (SSL) com " + host + ":" + dados.ImapPorta + ". A porta 993 usa SSL; a 143 normalmente não.";
        }
        catch (SocketException)
        {
            resposta.Mensagem = "Não foi possível conectar a " + host + ":" + dados.ImapPorta + ". Confira o servidor e a porta.";
        }
        catch (Exception erro)
        {
            resposta.Mensagem = "Não foi possível entrar na caixa: " + erro.Message;
        }

        return Resultado<ResultadoTesteEmailDto>.Ok(resposta);
    }
}
