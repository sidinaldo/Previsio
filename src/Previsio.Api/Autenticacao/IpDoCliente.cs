using Microsoft.AspNetCore.Http;

namespace Previsio.Api.Autenticacao;

/// <summary>
/// O IP de quem fez a requisição, para o limite de tentativas de login.
///
/// Atrás do Cloudflare Tunnel toda requisição chega pelo nginx, então o IP da conexão é sempre o
/// mesmo e o limite viraria um balde só para todo mundo. Com Proxy:CabecalhoIpReal preenchido
/// (CF-Connecting-IP em produção), o IP vem desse cabeçalho, que o Cloudflare reescreve sempre.
/// Só ligue isso quando a API não tiver porta aberta para a internet: sem o proxy na frente,
/// qualquer um escreveria o cabeçalho.
/// </summary>
public static class IpDoCliente
{
    public const string ChaveCabecalhoIpReal = "Proxy:CabecalhoIpReal";
    public const string Desconhecido = "desconhecido";

    public static string Obter(HttpContext contexto, string? cabecalhoIpReal)
    {
        if (!string.IsNullOrWhiteSpace(cabecalhoIpReal))
        {
            string valor = contexto.Request.Headers[cabecalhoIpReal].ToString();
            // Lista "cliente, proxy1, proxy2": vale o primeiro
            int virgula = valor.IndexOf(',');
            if (virgula >= 0)
            {
                valor = valor.Substring(0, virgula);
            }
            valor = valor.Trim();
            if (valor != "")
            {
                return valor;
            }
        }
        if (contexto.Connection.RemoteIpAddress != null)
        {
            return contexto.Connection.RemoteIpAddress.ToString();
        }
        return Desconhecido;
    }
}
