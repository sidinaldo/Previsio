using System.Net;
using Microsoft.AspNetCore.Http;
using Previsio.Api.Autenticacao;
using Xunit;

namespace Previsio.Tests.Api.Autenticacao;

public class IpDoClienteTests
{
    private static DefaultHttpContext Contexto(string? ipConexao)
    {
        var contexto = new DefaultHttpContext();
        if (ipConexao != null)
        {
            contexto.Connection.RemoteIpAddress = IPAddress.Parse(ipConexao);
        }
        return contexto;
    }

    [Fact]
    public void Sem_cabecalho_configurado_usa_o_ip_da_conexao()
    {
        DefaultHttpContext contexto = Contexto("172.18.0.5");
        contexto.Request.Headers["CF-Connecting-IP"] = "200.1.2.3";

        Assert.Equal("172.18.0.5", IpDoCliente.Obter(contexto, null));
    }

    [Fact]
    public void Com_cabecalho_configurado_usa_o_ip_real()
    {
        DefaultHttpContext contexto = Contexto("172.18.0.5");
        contexto.Request.Headers["CF-Connecting-IP"] = "200.1.2.3";

        Assert.Equal("200.1.2.3", IpDoCliente.Obter(contexto, "CF-Connecting-IP"));
    }

    [Fact]
    public void Lista_no_cabecalho_fica_com_o_primeiro()
    {
        DefaultHttpContext contexto = Contexto("172.18.0.5");
        contexto.Request.Headers["X-Forwarded-For"] = " 200.1.2.3 , 10.0.0.1";

        Assert.Equal("200.1.2.3", IpDoCliente.Obter(contexto, "X-Forwarded-For"));
    }

    [Fact]
    public void Cabecalho_ausente_cai_no_ip_da_conexao()
    {
        DefaultHttpContext contexto = Contexto("172.18.0.5");

        Assert.Equal("172.18.0.5", IpDoCliente.Obter(contexto, "CF-Connecting-IP"));
    }

    [Fact]
    public void Sem_nada_devolve_desconhecido()
    {
        Assert.Equal(IpDoCliente.Desconhecido, IpDoCliente.Obter(Contexto(null), "CF-Connecting-IP"));
    }
}
