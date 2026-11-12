using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Previsio.Api.Tests.Apoio;

/// <summary>
/// Certificado A1 de mentira (autoassinado), no formato do e-CNPJ: "RAZAO SOCIAL:CNPJ" no nome.
/// </summary>
public static class CertificadoDeTeste
{
    public const string Senha = "senha-do-certificado";

    public static byte[] Pfx(string cnpj, DateTimeOffset validoAte)
    {
        using (RSA chave = RSA.Create(2048))
        {
            var pedido = new CertificateRequest("CN=AUTO CENTER LTDA:" + cnpj + ", O=ICP-Brasil, C=BR", chave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            DateTimeOffset inicio = validoAte.AddYears(-1);
            using (X509Certificate2 certificado = pedido.CreateSelfSigned(inicio, validoAte))
            {
                return certificado.Export(X509ContentType.Pfx, Senha);
            }
        }
    }

    public static byte[] Pfx(string cnpj)
    {
        return Pfx(cnpj, DateTimeOffset.UtcNow.AddYears(1));
    }
}
