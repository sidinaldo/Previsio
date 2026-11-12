using Previsio.Core.Entidades;
using Previsio.Core.Contratos;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Aplicacao.Mapeamentos;

public static class ClienteMapeamento
{
    public static ClienteDto ParaDto(Cliente cliente)
    {
        return ParaDto(cliente, DateTime.UtcNow);
    }

    public static ClienteDto ParaDto(Cliente cliente, DateTime agoraUtc)
    {
        var dto = new ClienteDto();
        dto.Id = cliente.Id;
        dto.Nome = cliente.Nome;
        dto.ImapHost = cliente.ImapHost;
        dto.ImapPorta = cliente.ImapPorta;
        dto.ImapSsl = cliente.ImapSsl;
        dto.ImapUsuario = cliente.ImapUsuario;
        dto.BlingClientId = cliente.BlingClientId;
        dto.BlingClientSecretCadastrado = !string.IsNullOrEmpty(cliente.BlingClientSecretProtegido);
        dto.Contrato = PeriodoTeste.Situacao(cliente, agoraUtc);
        if (cliente.Contrato == SituacaoContrato.EmTeste && cliente.TesteTerminaEm.HasValue)
        {
            dto.TesteUltimoDia = PeriodoTeste.UltimoDia(cliente.TesteTerminaEm.Value);
            dto.DiasRestantesTeste = PeriodoTeste.DiasRestantes(cliente.TesteTerminaEm.Value, agoraUtc);
        }
        dto.Ativo = cliente.Ativo;
        return dto;
    }
}
