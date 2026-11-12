using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;

namespace Previsio.Api.Mapeamentos;

public static class EmpresaMapeamento
{
    /// <summary>
    /// Para dizer de onde vem o aplicativo do Bling, a empresa precisa vir com o Cliente (Include).
    /// </summary>
    public static EmpresaDto ParaDto(Empresa empresa)
    {
        var dto = new EmpresaDto();
        dto.Id = empresa.Id;
        dto.Nome = empresa.Nome;
        dto.Cnpj = empresa.Cnpj;
        dto.BlingConectado = empresa.BlingConectado;
        dto.BlingClientId = empresa.BlingClientId;
        dto.BlingClientSecretCadastrado = !string.IsNullOrEmpty(empresa.BlingClientSecretProtegido);
        dto.OrigemAplicativoBling = OrigemAplicativoBling.Da(empresa, empresa.Cliente);
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
        dto.Ativo = empresa.Ativo;
        return dto;
    }
}
