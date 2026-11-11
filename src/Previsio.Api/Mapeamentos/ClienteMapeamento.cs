using System.Linq.Expressions;
using Previsio.Api.Dtos;
using Previsio.Core.Entidades;

namespace Previsio.Api.Mapeamentos;

public static class ClienteMapeamento
{
    /// <summary>
    /// Mesma conversão de ParaDto, em forma que o EF traduz para SQL (só lê as colunas da tela).
    /// </summary>
    public static readonly Expression<Func<Cliente, ClienteDto>> Projecao = c => new ClienteDto
    {
        Id = c.Id,
        Nome = c.Nome,
        ImapHost = c.ImapHost,
        ImapPorta = c.ImapPorta,
        ImapSsl = c.ImapSsl,
        ImapUsuario = c.ImapUsuario,
        Ativo = c.Ativo
    };

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
}
