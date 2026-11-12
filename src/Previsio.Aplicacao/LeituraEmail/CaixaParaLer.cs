using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.LeituraEmail;

/// <summary>
/// Uma caixa de e-mail a ser lida: a da rede (Empresa nula) ou a própria de uma empresa.
/// As notas de qualquer caixa vão para a empresa da rede pelo CNPJ do destinatário.
/// </summary>
public class CaixaParaLer
{
    public Cliente Rede { get; private set; }
    public Empresa? Empresa { get; private set; }

    public CaixaParaLer(Cliente rede, Empresa? empresa)
    {
        Rede = rede;
        Empresa = empresa;
    }

    /// <summary>
    /// Servidor, usuário, senha e controle de UID da caixa.
    /// </summary>
    public ICaixaEmail Dados
    {
        get
        {
            if (Empresa != null)
            {
                return Empresa;
            }
            return Rede;
        }
    }

    public int? EmpresaId
    {
        get
        {
            if (Empresa != null)
            {
                return Empresa.Id;
            }
            return null;
        }
    }

    /// <summary>
    /// Para os logs: "rede 3" ou "empresa 12".
    /// </summary>
    public string Descricao
    {
        get
        {
            if (Empresa != null)
            {
                return "empresa " + Empresa.Id;
            }
            return "rede " + Rede.Id;
        }
    }
}
