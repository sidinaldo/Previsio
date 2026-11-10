using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Data;

namespace Previsio.Api.Controllers;

[ApiController]
[Route("api/clientes")]
public class ClientesController : ControllerBase
{
    private readonly PrevisioDbContext _db;
    private readonly ProtetorSegredos _protetor;

    public ClientesController(PrevisioDbContext db, ProtetorSegredos protetor)
    {
        _db = db;
        _protetor = protetor;
    }

    [HttpGet]
    [Authorize(Roles = Perfis.Admin)]
    public async Task<List<ClienteDto>> Listar(CancellationToken cancelamento)
    {
        return await _db.Clientes
            .OrderBy(c => c.Nome)
            .Select(c => new ClienteDto
            {
                Id = c.Id,
                Nome = c.Nome,
                ImapHost = c.ImapHost,
                ImapPorta = c.ImapPorta,
                ImapSsl = c.ImapSsl,
                ImapUsuario = c.ImapUsuario,
                Ativo = c.Ativo
            })
            .ToListAsync(cancelamento);
    }

    /// <summary>
    /// Admin vê qualquer cliente; o usuário de cliente, só o próprio.
    /// </summary>
    [HttpGet("{clienteId:int}")]
    public async Task<ActionResult<ClienteDto>> Obter(int clienteId, CancellationToken cancelamento)
    {
        // Cliente de outro usuário responde igual a inexistente, para não revelar ids
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound("Cliente não encontrado.");
        }

        Cliente? cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return NotFound("Cliente não encontrado.");
        }

        return ParaDto(cliente);
    }

    /// <summary>
    /// Altera os campos preenchidos. A senha do IMAP só troca se vier preenchida.
    /// Desativar o cliente derruba na hora as sessões dos usuários dele.
    /// </summary>
    [HttpPut("{clienteId:int}")]
    [Authorize(Roles = Perfis.Admin)]
    public async Task<ActionResult<ClienteDto>> Alterar(
        int clienteId, AlteracaoClienteDto dados, CancellationToken cancelamento)
    {
        Cliente? cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return NotFound("Cliente não encontrado.");
        }

        if (dados.Nome != null)
        {
            if (string.IsNullOrWhiteSpace(dados.Nome))
            {
                return BadRequest("Informe o nome do cliente.");
            }
            cliente.Nome = dados.Nome.Trim();
        }

        // Guardados para saber se a caixa de e-mail mudou
        string hostAnterior = cliente.ImapHost;
        string usuarioAnterior = cliente.ImapUsuario;

        if (dados.ImapHost != null)
        {
            cliente.ImapHost = dados.ImapHost.Trim();
        }
        if (dados.ImapPorta.HasValue)
        {
            cliente.ImapPorta = dados.ImapPorta.Value;
        }
        if (dados.ImapSsl.HasValue)
        {
            cliente.ImapSsl = dados.ImapSsl.Value;
        }
        if (dados.ImapUsuario != null)
        {
            cliente.ImapUsuario = dados.ImapUsuario.Trim();
        }
        if (!string.IsNullOrEmpty(dados.ImapSenha))
        {
            cliente.ImapSenhaProtegida = _protetor.Proteger(dados.ImapSenha);
        }
        if (dados.Ativo.HasValue)
        {
            cliente.Ativo = dados.Ativo.Value;
        }

        // Outra caixa de e-mail: os UIDs da caixa anterior não valem nela
        bool trocouCaixa = !string.Equals(hostAnterior, cliente.ImapHost, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(usuarioAnterior, cliente.ImapUsuario, StringComparison.OrdinalIgnoreCase);
        if (trocouCaixa)
        {
            cliente.ImapUidValidity = null;
            cliente.ImapUltimoUid = null;
        }

        await _db.SaveChangesAsync(cancelamento);
        return ParaDto(cliente);
    }

    [HttpPost]
    [Authorize(Roles = Perfis.Admin)]
    public async Task<ActionResult<ClienteDto>> Criar(NovoClienteDto dados, CancellationToken cancelamento)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
        {
            return BadRequest("Informe o nome do cliente.");
        }

        var cliente = new Cliente();
        cliente.Nome = dados.Nome.Trim();
        cliente.ImapHost = dados.ImapHost.Trim();
        cliente.ImapPorta = dados.ImapPorta;
        cliente.ImapSsl = dados.ImapSsl;
        cliente.ImapUsuario = dados.ImapUsuario.Trim();
        if (!string.IsNullOrEmpty(dados.ImapSenha))
        {
            cliente.ImapSenhaProtegida = _protetor.Proteger(dados.ImapSenha);
        }

        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync(cancelamento);

        return ParaDto(cliente);
    }

    [HttpGet("{clienteId:int}/empresas")]
    public async Task<ActionResult<List<EmpresaDto>>> ListarEmpresas(int clienteId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }

        List<Empresa> empresas = await _db.Empresas
            .Where(e => e.ClienteId == clienteId)
            .OrderBy(e => e.Nome)
            .ToListAsync(cancelamento);

        var lista = new List<EmpresaDto>();
        foreach (Empresa empresa in empresas)
        {
            lista.Add(ParaDto(empresa));
        }
        return lista;
    }

    [HttpPost("{clienteId:int}/empresas")]
    public async Task<ActionResult<EmpresaDto>> CriarEmpresa(
        int clienteId, NovaEmpresaDto dados, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }

        bool clienteExiste = await _db.Clientes.AnyAsync(c => c.Id == clienteId, cancelamento);
        if (!clienteExiste)
        {
            return NotFound("Cliente não encontrado.");
        }

        string cnpj = LeitorNFe.SomenteDigitos(dados.Cnpj);
        if (cnpj.Length != 14)
        {
            return BadRequest("CNPJ inválido.");
        }

        bool cnpjRepetido = await _db.Empresas.AnyAsync(e => e.ClienteId == clienteId && e.Cnpj == cnpj, cancelamento);
        if (cnpjRepetido)
        {
            return Conflict("Já existe uma empresa com esse CNPJ para este cliente.");
        }

        var empresa = new Empresa();
        empresa.ClienteId = clienteId;
        empresa.Nome = dados.Nome.Trim();
        empresa.Cnpj = cnpj;

        string clientId = (dados.BlingClientId ?? "").Trim();
        string clientSecret = (dados.BlingClientSecret ?? "").Trim();
        if (clientId != "" || clientSecret != "")
        {
            if (clientId == "" || clientSecret == "")
            {
                return BadRequest("Informe o Client ID e o Client Secret do aplicativo do Bling, ou deixe os dois vazios.");
            }
            empresa.BlingClientId = clientId;
            empresa.BlingClientSecretProtegido = _protetor.Proteger(clientSecret);
        }

        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync(cancelamento);

        return ParaDto(empresa);
    }

    public static ClienteDto ParaDto(Cliente cliente)
    {
        var dto = new ClienteDto();
        dto.Id = cliente.Id;
        dto.Nome = cliente.Nome;
        dto.ImapHost = cliente.ImapHost;
        dto.ImapPorta = cliente.ImapPorta;
        dto.ImapSsl = cliente.ImapSsl;
        dto.ImapUsuario = cliente.ImapUsuario;
        dto.Ativo = cliente.Ativo;
        return dto;
    }

    public static EmpresaDto ParaDto(Empresa empresa)
    {
        var dto = new EmpresaDto();
        dto.Id = empresa.Id;
        dto.Nome = empresa.Nome;
        dto.Cnpj = empresa.Cnpj;
        dto.BlingConectado = empresa.BlingConectado;
        dto.BlingClientId = empresa.BlingClientId;
        dto.BlingClientSecretCadastrado = !string.IsNullOrEmpty(empresa.BlingClientSecretProtegido);
        dto.UltimaSincronizacaoEm = empresa.UltimaSincronizacaoEm;
        dto.Ativo = empresa.Ativo;
        return dto;
    }
}
