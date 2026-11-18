using Previsio.Core.Entidades;
using Previsio.Aplicacao.Bling;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Aplicacao.Mapeamentos;

public static class EmpresaMapeamento
{
    /// <summary>
    /// Para dizer de onde vem o aplicativo do ERP, a empresa precisa vir com o Cliente (Include).
    /// </summary>
    public static EmpresaDto ParaDto(Empresa empresa)
    {
        var dto = new EmpresaDto();
        dto.Id = empresa.Id;
        dto.Nome = empresa.Nome;
        dto.Cnpj = empresa.Cnpj;
        dto.ErpConectado = empresa.ErpConectado;
        dto.ErpNome = NomeErp.De(empresa.Erp);
        dto.ErpClientId = empresa.ErpClientId;
        dto.ErpClientSecretCadastrado = !string.IsNullOrEmpty(empresa.ErpClientSecretProtegido);
        dto.OrigemAplicativoErp = OrigemAplicativoErp.Da(empresa, empresa.Cliente);
        dto.EmailProprio = empresa.TemEmailProprio;
        dto.ImapHost = empresa.ImapHost;
        dto.ImapPorta = empresa.ImapPorta;
        dto.ImapSsl = empresa.ImapSsl;
        dto.ImapUsuario = empresa.ImapUsuario;
        dto.ImapSenhaCadastrada = !string.IsNullOrEmpty(empresa.ImapSenhaProtegida);
        dto.UltimaSincronizacaoEm = empresa.UltimaSincronizacaoEm;
        dto.CertificadoCadastrado = empresa.TemCertificado;
        dto.CertificadoTitular = empresa.CertificadoTitular;
        dto.CertificadoValidoAte = empresa.CertificadoValidoAte;
        dto.Uf = empresa.Uf;
        dto.SefazBuscaAtiva = empresa.SefazBuscaAtiva;
        dto.SefazUltimaBuscaEm = empresa.SefazUltimaBuscaEm;
        dto.SefazSituacao = empresa.SefazSituacao;
        dto.Ativo = empresa.Ativo;
        return dto;
    }
}
