using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace Previsio.Core.Sefaz;

/// <summary>
/// Abre o certificado digital A1 (.pfx) e lê o CNPJ do titular (e-CNPJ ICP-Brasil: "RAZAO SOCIAL:CNPJ" no CN).
/// </summary>
public static class CertificadoA1
{
    public const string MensagemSenhaInvalida = "Não consegui abrir o certificado: confira o arquivo .pfx e a senha.";
    public const string MensagemSemChavePrivada = "O arquivo não traz a chave privada do certificado. Exporte o A1 de novo, com a chave.";

    private static readonly Regex CnpjNoNome = new Regex(@"(?<!\d)(\d{14})(?!\d)", RegexOptions.Compiled);

    /// <summary>
    /// Abre o .pfx. Lança CryptographicException quando a senha ou o arquivo não servem.
    /// </summary>
    public static X509Certificate2 Abrir(byte[] pfx, string senha)
    {
        // MachineKeySet: a chave fica disponível para o TLS do Windows mesmo sem perfil de usuário carregado (IIS, serviço)
        X509KeyStorageFlags opcoes = X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable;
        X509Certificate2 certificado = new X509Certificate2(pfx, senha, opcoes);
        if (!certificado.HasPrivateKey)
        {
            certificado.Dispose();
            throw new CryptographicException(MensagemSemChavePrivada);
        }
        return certificado;
    }

    /// <summary>
    /// CNPJ do titular: o número de 14 dígitos no nome do certificado; nulo se não houver.
    /// </summary>
    public static string? Cnpj(X509Certificate2 certificado)
    {
        Match encontrado = CnpjNoNome.Match(certificado.Subject);
        if (encontrado.Success)
        {
            return encontrado.Groups[1].Value;
        }
        return null;
    }

    /// <summary>
    /// Nome do titular (o CN sem o CNPJ), para mostrar na tela.
    /// </summary>
    public static string Titular(X509Certificate2 certificado)
    {
        string nome = certificado.GetNameInfo(X509NameType.SimpleName, false);
        int separador = nome.LastIndexOf(':');
        if (separador > 0)
        {
            return nome.Substring(0, separador);
        }
        return nome;
    }
}
