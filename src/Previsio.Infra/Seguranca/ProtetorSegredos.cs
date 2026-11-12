using Microsoft.AspNetCore.DataProtection;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Infra.Seguranca;

/// <summary>
/// Criptografa tokens do Bling e senhas de IMAP antes de gravar no banco (ASP.NET Data Protection).
/// </summary>
public class ProtetorSegredos : IProtetorSegredos
{
    private readonly IDataProtector _protetor;

    public ProtetorSegredos(IDataProtectionProvider provedor)
    {
        _protetor = provedor.CreateProtector("Previsio.Segredos.v1");
    }

    public string Proteger(string valor)
    {
        return _protetor.Protect(valor);
    }

    public string Revelar(string valorProtegido)
    {
        return _protetor.Unprotect(valorProtegido);
    }
}
