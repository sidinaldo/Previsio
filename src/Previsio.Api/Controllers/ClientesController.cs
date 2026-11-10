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
                ImapUsuario = c.ImapUsuario,
                Ativo = c.Ativo
            })
            .ToListAsync(cancelamento);
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

        var resultado = new ClienteDto();
        resultado.Id = cliente.Id;
        resultado.Nome = cliente.Nome;
        resultado.ImapHost = cliente.ImapHost;
        resultado.ImapUsuario = cliente.ImapUsuario;
        resultado.Ativo = cliente.Ativo;
        return resultado;
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

        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync(cancelamento);

        return ParaDto(empresa);
    }

    public static EmpresaDto ParaDto(Empresa empresa)
    {
        var dto = new EmpresaDto();
        dto.Id = empresa.Id;
        dto.Nome = empresa.Nome;
        dto.Cnpj = empresa.Cnpj;
        dto.BlingConectado = empresa.BlingConectado;
        dto.UltimaSincronizacaoEm = empresa.UltimaSincronizacaoEm;
        return dto;
    }
}
