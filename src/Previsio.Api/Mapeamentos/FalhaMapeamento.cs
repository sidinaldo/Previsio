using Previsio.Api.Dtos;
using Previsio.Core.Entidades;

namespace Previsio.Api.Mapeamentos;

public static class FalhaMapeamento
{
    public static FalhaDto ParaDto(FalhaProcessamento falha)
    {
        var dto = new FalhaDto();
        dto.Id = falha.Id;
        dto.OcorridaEm = falha.OcorridaEm;
        dto.Etapa = falha.Etapa.ToString();
        dto.Mensagem = falha.Mensagem;
        dto.Metodo = falha.Metodo;
        dto.Endereco = falha.Endereco;
        dto.StatusHttp = falha.StatusHttp;
        dto.Resposta = falha.Resposta;
        dto.EmpresaId = falha.EmpresaId;
        if (falha.Empresa != null)
        {
            dto.EmpresaNome = falha.Empresa.Nome;
        }
        dto.NotaFiscalId = falha.NotaFiscalId;
        dto.NumeroNota = falha.NumeroNota;
        dto.ChaveAcesso = falha.ChaveAcesso;
        dto.Fornecedor = falha.Fornecedor;
        dto.CnpjFornecedor = falha.CnpjFornecedor;
        dto.ValorNota = falha.ValorNota;
        dto.NumeroParcela = falha.NumeroParcela;
        dto.ValorParcela = falha.ValorParcela;
        dto.VencimentoParcela = falha.VencimentoParcela;
        dto.Origem = falha.Origem;
        return dto;
    }
}
