using Microsoft.EntityFrameworkCore;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Data;

namespace Previsio.Api.Servicos;

/// <summary>
/// Grava a nota lida do e-mail, identificando a empresa pelo CNPJ do destinatário.
/// Não fala com o Bling: o lançamento acontece depois, no job de lançamento.
/// </summary>
public class RegistroNotaService
{
    private readonly PrevisioDbContext _db;

    public RegistroNotaService(PrevisioDbContext db)
    {
        _db = db;
    }

    public async Task<ResultadoRegistro> RegistrarAsync(
        int clienteId, NotaFiscalXml xml, string origemEmail, CancellationToken cancelamento)
    {
        bool jaExiste = await _db.NotasFiscais
            .AnyAsync(n => n.ClienteId == clienteId && n.ChaveAcesso == xml.ChaveAcesso, cancelamento);

        if (jaExiste)
        {
            return ResultadoRegistro.Duplicada;
        }

        Empresa? empresa = await _db.Empresas
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

        _db.NotasFiscais.Add(nota);

        try
        {
            await _db.SaveChangesAsync(cancelamento);
        }
        catch (DbUpdateException)
        {
            // Outra execução gravou a mesma chave ao mesmo tempo (índice único).
            // Limpa o rastreamento para a nota e as parcelas não serem reenviadas no próximo SaveChanges.
            _db.ChangeTracker.Clear();
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
