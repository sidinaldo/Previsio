using Microsoft.AspNetCore.DataProtection;

namespace Previsio.Api.Servicos;

/// <summary>
/// Criptografa tokens do Bling e senhas de IMAP antes de gravar no banco.
/// </summary>
public class ProtetorSegredos
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
