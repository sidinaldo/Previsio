using Previsio.Api.Dtos;
using Previsio.Core.Entidades;

namespace Previsio.Api.Mapeamentos;

public static class NotaMapeamento
{
    /// <summary>
    /// Linha da tela de notas pendentes.
    /// </summary>
    public static NotaDto ParaNotaDto(NotaFiscal nota)
    {
        var dto = new NotaDto();
        dto.Id = nota.Id;
        dto.EmpresaId = nota.EmpresaId;
        if (nota.Empresa != null)
        {
            dto.EmpresaNome = nota.Empresa.Nome;
        }
        dto.Numero = nota.Numero;
        dto.ChaveAcesso = nota.ChaveAcesso;
        dto.Fornecedor = nota.NomeFornecedor;
        dto.CnpjFornecedor = nota.CnpjFornecedor;
        dto.DataEmissao = nota.DataEmissao;
        dto.ValorTotal = nota.ValorTotal;
        if (nota.Parcelas.Count > 0)
        {
            dto.PrimeiroVencimento = nota.Parcelas.Min(p => p.Vencimento);
        }
        dto.Status = nota.Status.ToString();
        dto.MensagemErro = nota.MensagemErro;
        dto.RecebidaEm = nota.RecebidaEm;
        dto.ResolvidaManualmenteEm = nota.ResolvidaManualmenteEm;
        dto.ResolvidaManualmentePor = nota.ResolvidaManualmentePor;
        dto.ObservacaoResolucao = nota.ObservacaoResolucao;
        dto.CaixaDeOutraEmpresa = CaixaDeOutraEmpresa(nota);
        return dto;
    }

    /// <summary>
    /// Nota recebida com as parcelas, para a tela de notas recebidas.
    /// </summary>
    public static NotaRecebidaDto ParaNotaRecebidaDto(NotaFiscal nota)
    {
        var dto = new NotaRecebidaDto();
        dto.Id = nota.Id;
        dto.EmpresaId = nota.EmpresaId;
        if (nota.Empresa != null)
        {
            dto.EmpresaNome = nota.Empresa.Nome;
        }
        dto.Numero = nota.Numero;
        dto.Serie = nota.Serie;
        dto.ChaveAcesso = nota.ChaveAcesso;
        dto.Fornecedor = nota.NomeFornecedor;
        dto.CnpjFornecedor = nota.CnpjFornecedor;
        dto.DataEmissao = nota.DataEmissao;
        dto.ValorTotal = nota.ValorTotal;
        dto.Status = nota.Status.ToString();
        dto.MensagemErro = nota.MensagemErro;
        dto.RecebidaEm = nota.RecebidaEm;
        dto.LancadaEm = nota.LancadaEm;
        dto.ResolvidaManualmenteEm = nota.ResolvidaManualmenteEm;
        dto.ResolvidaManualmentePor = nota.ResolvidaManualmentePor;
        dto.ObservacaoResolucao = nota.ObservacaoResolucao;
        dto.CaixaDeOutraEmpresa = CaixaDeOutraEmpresa(nota);

        List<ParcelaNota> parcelas = nota.Parcelas
            .OrderBy(p => p.Vencimento)
            .ThenBy(p => p.NumeroParcela)
            .ToList();
        if (parcelas.Count > 0)
        {
            dto.PrimeiroVencimento = parcelas[0].Vencimento;
        }
        foreach (ParcelaNota parcela in parcelas)
        {
            var parcelaDto = new ParcelaNotaDto();
            parcelaDto.NumeroParcela = parcela.NumeroParcela;
            parcelaDto.Vencimento = parcela.Vencimento;
            parcelaDto.Valor = parcela.Valor;
            parcelaDto.BlingContaPagarId = parcela.BlingContaPagarId;
            dto.Parcelas.Add(parcelaDto);
        }
        return dto;
    }

    /// <summary>
    /// A nota chegou na caixa própria de uma empresa, mas o CNPJ do XML é de outra (ou de nenhuma).
    /// </summary>
    public static string? CaixaDeOutraEmpresa(NotaFiscal nota)
    {
        if (nota.EmpresaCaixaId == null || nota.EmpresaCaixa == null)
        {
            return null;
        }
        if (nota.EmpresaId == nota.EmpresaCaixaId)
        {
            return null;
        }
        return nota.EmpresaCaixa.Nome;
    }
}
