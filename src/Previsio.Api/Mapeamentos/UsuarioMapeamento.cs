using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Core.Entidades;

namespace Previsio.Api.Mapeamentos;

public static class UsuarioMapeamento
{
    public static UsuarioDto ParaDto(Usuario usuario)
    {
        var dto = new UsuarioDto();
        dto.Id = usuario.Id;
        dto.ClienteId = usuario.ClienteId;
        dto.Nome = usuario.Nome;
        dto.Email = usuario.Email;
        dto.Perfil = Perfis.Nome(usuario.Perfil);
        dto.Ativo = usuario.Ativo;
        dto.UltimoAcessoEm = usuario.UltimoAcessoEm;
        return dto;
    }
}
