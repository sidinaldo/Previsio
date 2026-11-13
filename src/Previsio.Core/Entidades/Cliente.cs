namespace Previsio.Core.Entidades;

/// <summary>
/// Cliente do Previsio (tenant, a rede). Ex.: o grupo de autopeças com 5 CNPJs.
/// </summary>
public class Cliente : ICaixaEmail
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";

    // Caixa de e-mail dedicada que recebe os XMLs encaminhados pelo cliente
    public string ImapHost { get; set; } = "";
    public int ImapPorta { get; set; } = 993;
    public bool ImapSsl { get; set; } = true;
    public string ImapUsuario { get; set; } = "";
    public string ImapSenhaProtegida { get; set; } = "";

    // Controle de leitura independente de "lido/não lido": UIDVALIDITY da caixa de entrada
    // e UID do último e-mail já processado (nulos até a primeira leitura)
    public long? ImapUidValidity { get; set; }
    public long? ImapUltimoUid { get; set; }
    public DateTime? ImapUltimaConexaoEm { get; set; }
    public DateTime? ImapUltimaFalhaConexaoEm { get; set; }
    public string? ImapUltimoErroConexao { get; set; }

    // Aplicativo privado do Bling da rede: vale para as empresas que não têm um próprio.
    // Vazio: as empresas sem aplicativo próprio usam o aplicativo global do Previsio (appsettings).
    public string? BlingClientId { get; set; }
    public string? BlingClientSecretProtegido { get; set; }

    // Resumo semanal por e-mail para os usuários da rede. Dia da semana como DayOfWeek (0 = domingo) ou 7 = todo dia útil
    // e hora cheia no horário de Brasília. EnviadoEm evita mandar duas vezes no mesmo dia.
    public bool ResumoSemanalAtivo { get; set; } = true;
    public int ResumoSemanalDia { get; set; } = (int)DayOfWeek.Monday;
    public int ResumoSemanalHora { get; set; } = 7;
    public DateTime? ResumoSemanalEnviadoEm { get; set; }

    // Contas vencidas: viram aviso depois de N dias de atraso (1 = no dia seguinte ao vencimento).
    // Alerta diário: e-mail com as contas que vencem hoje e amanhã, na hora escolhida (Brasília).
    public int AvisoVencidasDias { get; set; } = 1;
    public bool AlertaVencimentosAtivo { get; set; } = true;
    public int AlertaVencimentosHora { get; set; } = 7;
    public DateTime? AlertaVencimentosEnviadoEm { get; set; }

    // Conferência com o Bling: antes de lançar, procura a conta já lançada à mão (mesmo fornecedor,
    // vencimento e valor, com as tolerâncias); e fecha sozinhas as notas pendentes encontradas lá.
    public bool ConferirBlingAntesDeLancar { get; set; } = true;

    // Fluxo de caixa: traz as contas a receber do Bling e mostra entradas × saídas no painel.
    // Desligado para quem não lança as vendas no Bling (as entradas ficariam zeradas).
    public bool MostrarFluxoCaixa { get; set; } = true;

    // Categoria (nome) das notas de fornecedor sem regra e sem histórico no Bling; vazio = sem categoria
    public string? CategoriaPadrao { get; set; }
    public bool ConferenciaExigeNumeroNota { get; set; }
    public decimal ConferenciaToleranciaValor { get; set; } = 0.10m;
    public int ConferenciaToleranciaDias { get; set; }
    public bool ConferenciaFecharPendentes { get; set; } = true;

    // Teste grátis: termina no início do dia seguinte ao último dia (UTC). Acabado o teste sem contratar,
    // o Previsio para de ler e-mails e lançar no Bling; os usuários continuam vendo os dados.
    public SituacaoContrato Contrato { get; set; } = SituacaoContrato.Contratado;
    public DateTime? TesteTerminaEm { get; set; }
    public DateTime? TesteAvisoFinalEnviadoEm { get; set; }
    public DateTime? TesteAvisoEncerradoEnviadoEm { get; set; }
    public DateTime? ContratadoEm { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<Empresa> Empresas { get; set; } = new List<Empresa>();
}
