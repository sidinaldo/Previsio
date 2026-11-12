using Microsoft.EntityFrameworkCore;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;

namespace Previsio.Api.Servicos;

/// <summary>
/// Grava a nota lida do e-mail, identificando a empresa pelo CNPJ do destinatário.
/// Não fala com o Bling: o lançamento acontece depois, no job de lançamento.
/// </summary>
public class RegistroNotaService
{
    private readonly IRepositorio<NotaFiscal> _notas;
    private readonly IRepositorio<Empresa> _empresas;

    public RegistroNotaService(IRepositorio<NotaFiscal> notas, IRepositorio<Empresa> empresas)
    {
        _notas = notas;
        _empresas = empresas;
    }

    /// <param name="empresaDaCaixaId">Empresa dona da caixa em que a nota chegou; nula para a caixa da rede.</param>
    public async Task<ResultadoRegistro> RegistrarAsync(
        int clienteId, NotaFiscalXml xml, string origemEmail, int? empresaDaCaixaId, CancellationToken cancelamento)
    {
        bool jaExiste = await _notas.Consultar()
            .AnyAsync(n => n.ClienteId == clienteId && n.ChaveAcesso == xml.ChaveAcesso, cancelamento);

        if (jaExiste)
        {
            return ResultadoRegistro.Duplicada;
        }

        Empresa? empresa = await _empresas.Consultar()
            .FirstOrDefaultAsync(e => e.ClienteId == clienteId && e.Cnpj == xml.CnpjDestinatario && e.Ativo, cancelamento);

        var nota = new NotaFiscal();
        nota.ClienteId = clienteId;
        nota.ChaveAcesso = xml.ChaveAcesso;
        nota.Numero = xml.Numero;
        nota.Serie = xml.Serie;
        nota.DataEmissao = xml.DataEmissao;
        nota.ValorTotal = xml.ValorTotal;
        nota.CnpjDestinatario = xml.CnpjDestinatario;
        nota.CnpjFornecedor = xml.CnpjFornecedor;
        nota.NomeFornecedor = Limitar(xml.NomeFornecedor, 200);
        nota.OrigemEmail = Limitar(origemEmail, 300);
        nota.EmpresaCaixaId = empresaDaCaixaId;
        nota.RecebidaEm = DateTime.UtcNow;

        if (empresa == null)
        {
            nota.Status = StatusNota.EmpresaNaoEncontrada;
            nota.MensagemErro = "Nenhuma empresa cadastrada com o CNPJ " + xml.CnpjDestinatario + ".";
        }
        else if (xml.Duplicatas.Count == 0)
        {
            nota.EmpresaId = empresa.Id;
            nota.Status = StatusNota.SemParcelas;
            nota.MensagemErro = "A nota não traz duplicatas (vencimentos). Lançar manualmente no Bling.";
        }
        else
        {
            nota.EmpresaId = empresa.Id;
            nota.Status = StatusNota.Recebida;
        }

        foreach (DuplicataXml duplicata in xml.Duplicatas)
        {
            var parcela = new ParcelaNota();
            parcela.NumeroParcela = duplicata.Numero;
            parcela.Vencimento = duplicata.Vencimento;
            parcela.Valor = duplicata.Valor;
            nota.Parcelas.Add(parcela);
        }

        _notas.Adicionar(nota);

        try
        {
            await _notas.SalvarAsync(cancelamento);
        }
        catch (DbUpdateException)
        {
            // Outra execução gravou a mesma chave ao mesmo tempo (índice único).
            // Limpa o rastreamento para a nota e as parcelas não serem reenviadas no próximo SaveChanges.
            _notas.DescartarAlteracoes();
            return ResultadoRegistro.Duplicada;
        }

        return ResultadoRegistro.Registrada;
    }

    private static string Limitar(string texto, int tamanho)
    {
        if (texto.Length <= tamanho)
        {
            return texto;
        }
        return texto.Substring(0, tamanho);
    }
}
