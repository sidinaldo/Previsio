using Previsio.Core.Entidades;
using Previsio.Core.Contratos;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Aplicacao.Mapeamentos;

public static class ClienteMapeamento
{
    /// <summary>
    /// erpsDasEmpresas: o ERP de cada empresa da rede, para o nome que as telas usam (NomeErp.DaRede).
    /// </summary>
    public static ClienteDto ParaDto(Cliente cliente, IEnumerable<TipoErp> erpsDasEmpresas)
    {
        return ParaDto(cliente, erpsDasEmpresas, DateTime.UtcNow);
    }

    public static ClienteDto ParaDto(Cliente cliente, IEnumerable<TipoErp> erpsDasEmpresas, DateTime agoraUtc)
    {
        var dto = new ClienteDto();
        dto.Id = cliente.Id;
        dto.Nome = cliente.Nome;
        dto.ImapHost = cliente.ImapHost;
        dto.ImapPorta = cliente.ImapPorta;
        dto.ImapSsl = cliente.ImapSsl;
        dto.ImapUsuario = cliente.ImapUsuario;
        dto.ErpClientId = cliente.ErpClientId;
        dto.ErpClientSecretCadastrado = !string.IsNullOrEmpty(cliente.ErpClientSecretProtegido);
        dto.ErpNome = NomeErp.DaRede(erpsDasEmpresas);
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
