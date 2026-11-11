using Previsio.Api.Dtos;
using Previsio.Core.Entidades;

namespace Previsio.Api.Mapeamentos;

public static class EmpresaMapeamento
{
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
