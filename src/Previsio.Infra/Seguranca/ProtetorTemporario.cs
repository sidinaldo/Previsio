using Microsoft.AspNetCore.DataProtection;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Infra.Seguranca;

/// <summary>
/// Assinatura com validade (ASP.NET Data Protection), usada no "state" do OAuth do Bling.
/// O propósito é o mesmo de antes, então links de conexão já gerados continuam válidos.
/// </summary>
public class ProtetorTemporario : IProtetorTemporario
{
    private readonly ITimeLimitedDataProtector _protetor;

    public ProtetorTemporario(IDataProtectionProvider provedor)
    {
        _protetor = provedor.CreateProtector("Previsio.OAuthState").ToTimeLimitedDataProtector();
    }

    public string Proteger(string valor, TimeSpan validade)
    {
        return _protetor.Protect(valor, validade);
    }

    public string Revelar(string valorProtegido)
    {
        return _protetor.Unprotect(valorProtegido);
    }
}
