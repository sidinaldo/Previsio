export interface TotalSemana {
  inicio: string;
  fim: string;
  valor: number;
}

export interface TotalEmpresa {
  empresaId: number;
  nome: string;
  cnpj: string;
  quantidadeContas: number;
  valor: number;
  notasPendentes: number;
  valorNotasPendentes: number;
  proximoVencimento: string | null;
  valorProximoVencimento: number;
  ultimaSincronizacaoEm: string | null;
}

export interface ResumoPainel {
  inicio: string;
  fim: string;
  totalPeriodo: number;
  quantidadeContas: number;
  totalSemanaAtual: number;
  notasPendentes: number;
  valorNotasPendentes: number;
  /** Contas em aberto no Bling com vencimento antes de hoje. */
  totalVencidas?: number;
  quantidadeVencidas?: number;
  semanas: TotalSemana[];
  empresas: TotalEmpresa[];
}

export interface VencimentoDia {
  data: string;
  quantidade: number;
  valor: number;
}

export interface Nota {
  id: number;
  empresaId: number | null;
  empresaNome: string | null;
  numero: string;
  chaveAcesso: string;
  fornecedor: string;
  cnpjFornecedor: string;
  dataEmissao: string;
  valorTotal: number;
  primeiroVencimento: string | null;
  status: string;
  mensagemErro: string | null;
  recebidaEm: string;
  /**
   * Nome da empresa dona da caixa em que a nota chegou, quando não é a do CNPJ da nota
   * (o fornecedor mandou para o e-mail de outra loja). A nota vai pelo CNPJ; isto só avisa.
   */
  caixaDeOutraEmpresa?: string | null;
}

/** Preenchidos quando alguém marcou a nota como lançada manualmente no Bling. */
export interface ResolucaoManual {
  resolvidaManualmenteEm: string | null;
  resolvidaManualmentePor: string | null;
  observacaoResolucao: string | null;
}

export interface Empresa {
  id: number;
  nome: string;
  cnpj: string;
  blingConectado: boolean;
  /** Aplicativo privado criado na conta Bling desta empresa; null usa o da rede ou, sem ele, o padrão do Previsio. */
  blingClientId: string | null;
  /** O secret nunca volta da API, só se está cadastrado. */
  blingClientSecretCadastrado: boolean;
  /** De onde vem o aplicativo do Bling em uso. */
  origemAplicativoBling: OrigemAplicativoBling;
  /** Caixa de e-mail própria; sem ela, as notas chegam só pela caixa da rede. */
  emailProprio: boolean;
  imapHost: string;
  imapPorta: number;
  imapSsl: boolean;
  imapUsuario: string;
  /** A senha nunca volta da API, só se está cadastrada. */
  imapSenhaCadastrada: boolean;
  ultimaSincronizacaoEm: string | null;
  /** Certificado digital A1, para buscar na SEFAZ o XML das notas que chegam só em PDF. */
  certificadoCadastrado?: boolean;
  certificadoTitular?: string | null;
  certificadoValidoAte?: string | null;
  uf?: string | null;
  ativo: boolean;
}

export type OrigemAplicativoBling = 'Empresa' | 'Rede' | 'Padrao';

export const DESCRICAO_ORIGEM_APLICATIVO: Record<OrigemAplicativoBling, string> = {
  Empresa: 'Aplicativo próprio',
  Rede: 'Aplicativo da rede',
  Padrao: 'Aplicativo padrão'
};

/** Caixa de e-mail própria da empresa, no cadastro e na alteração. */
export interface EmailEmpresa {
  /** "" remove a caixa própria (na alteração). */
  imapHost?: string;
  imapPorta?: number;
  imapSsl?: boolean;
  imapUsuario?: string;
  /** Vazio ou ausente mantém a senha atual. */
  imapSenha?: string;
}

/** POST /api/clientes/{id}/empresas. Client ID e Secret são opcionais, mas vão juntos. */
export interface NovaEmpresa extends EmailEmpresa {
  nome: string;
  cnpj: string;
  blingClientId?: string;
  blingClientSecret?: string;
}

/** PUT /api/empresas/{id}: só os campos enviados mudam. O CNPJ não pode ser alterado. */
export interface AlteracaoEmpresa extends EmailEmpresa {
  nome?: string;
  ativo?: boolean;
  /** "" volta ao aplicativo da rede (ou ao padrão). Trocar o Client ID desconecta a empresa. */
  blingClientId?: string;
  /** Vazio ou ausente mantém o secret atual. */
  blingClientSecret?: string;
}

export interface Cliente {
  id: number;
  nome: string;
  imapHost: string;
  imapPorta: number;
  imapSsl: boolean;
  imapUsuario: string;
  /** Aplicativo privado do Bling da rede, usado pelas empresas que não têm um próprio. */
  blingClientId: string | null;
  /** O secret nunca volta da API, só se está cadastrado. */
  blingClientSecretCadastrado: boolean;
  /** Teste grátis: "EmTeste" com os dias que faltam (contando hoje) e o último dia; "TesteEncerrado" parou de processar. */
  contrato?: SituacaoContrato;
  testeUltimoDia?: string | null;
  diasRestantesTeste?: number | null;
  ativo: boolean;
}

export type SituacaoContrato = 'Contratado' | 'EmTeste' | 'TesteEncerrado';

/** PUT /api/clientes/{id}: só os campos enviados mudam; a senha só troca se vier preenchida. */
export interface AlteracaoCliente {
  nome?: string;
  imapHost?: string;
  imapPorta?: number;
  imapSsl?: boolean;
  imapUsuario?: string;
  imapSenha?: string;
  /** "" remove o aplicativo da rede. Trocar o Client ID desconecta as empresas que usam o da rede. */
  blingClientId?: string;
  /** Vazio ou ausente mantém o secret atual. */
  blingClientSecret?: string;
  ativo?: boolean;
}

/** Cadastro de cliente (POST /api/clientes): a caixa de e-mail dedicada onde chegam as notas. */
export interface NovoCliente {
  nome: string;
  imapHost: string;
  imapPorta: number;
  imapSsl: boolean;
  imapUsuario: string;
  imapSenha: string;
  /** Opcionais: aplicativo privado do Bling da rede. Vão juntos ou nenhum. */
  blingClientId?: string;
  blingClientSecret?: string;
  /** Começa no teste grátis de 15 dias. */
  emTeste?: boolean;
}

/** POST /api/email/testar: dados da caixa digitados; sem senha e com clienteId usa a senha gravada da rede. */
export interface TesteEmail {
  imapHost: string;
  imapPorta: number;
  imapSsl: boolean;
  imapUsuario: string;
  imapSenha?: string;
  clienteId?: number;
}

/** Se o Previsio conseguiu entrar na caixa e, quando não, o motivo. */
export interface ResultadoTesteEmail {
  conectou: boolean;
  mensagem: string;
}

export const PERFIL_ADMIN = 'Admin';

export interface Usuario {
  id: number;
  // Vazio para o admin, que enxerga todos os clientes
  clienteId: number | null;
  nome: string;
  email: string;
  perfil: string;
  ativo: boolean;
  ultimoAcessoEm: string | null;
}

export const PERFIL_CLIENTE = 'Cliente';

/** POST /api/usuarios. O cliente é obrigatório para o perfil Cliente e fica vazio para Admin. */
export interface NovoUsuario {
  nome: string;
  email: string;
  senha: string;
  perfil: string;
  clienteId: number | null;
}

/** PUT /api/usuarios/{id}: só os campos enviados mudam. */
export interface AlteracaoUsuario {
  nome?: string;
  ativo?: boolean;
  novaSenha?: string;
}

export interface RespostaLogin {
  token: string;
  expiraEm: string;
  usuario: Usuario;
}

/** Parcela da nota; blingContaPagarId vem preenchido depois que a conta a pagar foi criada no Bling. */
export interface ParcelaNota {
  numeroParcela: string;
  vencimento: string;
  valor: number;
  blingContaPagarId: number | null;
}

/** Nota lida do e-mail, lançada ou não no Bling (GET /api/clientes/{id}/notas). */
export interface NotaRecebida extends ResolucaoManual {
  id: number;
  empresaId: number | null;
  empresaNome: string | null;
  numero: string;
  serie: string;
  chaveAcesso: string;
  fornecedor: string;
  cnpjFornecedor: string;
  dataEmissao: string;
  valorTotal: number;
  primeiroVencimento: string | null;
  status: string;
  mensagemErro: string | null;
  recebidaEm: string;
  lancadaEm: string | null;
  parcelas: ParcelaNota[];
  /**
   * Nome da empresa dona da caixa em que a nota chegou, quando não é a do CNPJ da nota
   * (o fornecedor mandou para o e-mail de outra loja). A nota vai pelo CNPJ; isto só avisa.
   */
  caixaDeOutraEmpresa?: string | null;
}

export interface PaginaNotas {
  itens: NotaRecebida[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
}

/** E-mail com anexo que a leitura examinou e não virou nota (tela Saúde). */
export interface EmailIgnorado {
  id: number;
  clienteId: number;
  clienteNome: string;
  // "Caixa da rede" ou o nome da empresa dona da caixa
  caixa: string;
  remetente: string;
  assunto: string;
  enviadoEm: string | null;
  lidoEm: string;
  anexos: string;
  motivo: string;
  temPdf: boolean;
  tratadoEm: string | null;
  tratadoPor: string | null;
  resultadoTratamento: string | null;
}

export interface TratamentoEmailIgnorado {
  mensagem: string;
  email: EmailIgnorado;
}

/** Conta a pagar do Bling (lançada pelo Previsio ou à mão), na tela Contas a pagar. */
export interface ContaPagar {
  id: number;
  blingId: number;
  empresaId: number;
  empresaNome: string;
  vencimento: string;
  valor: number;
  situacao: number;
  situacaoTexto: string;
  vencida: boolean;
  fornecedor: string | null;
  // 'Previsio' (veio de nota lida do e-mail) ou 'Bling' (lançada direto no Bling)
  origem: string;
  notaFiscalId: number | null;
  numeroNota: string | null;
  numeroParcela: string | null;
}

export interface PaginaContasPagar {
  itens: ContaPagar[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
  valorTotal: number;
  sincronizadaEm: string | null;
  diasPassados: number;
  diasFuturos: number;
}

/** Filtros da tela Contas a pagar. Datas de vencimento no formato yyyy-MM-dd. */
export interface FiltroContas {
  empresaId?: number | null;
  // Abertas, Vencidas, Pagas, Canceladas ou Todas
  situacao?: string;
  // Previsio, Bling ou vazio (as duas)
  origem?: string;
  de?: string;
  ate?: string;
  // Fornecedor, NF ou empresa
  busca?: string;
  ordem?: string;
  pagina?: number;
}

/** Filtros da tela de notas recebidas. Datas (dia em que a nota chegou) no formato yyyy-MM-dd. */
export interface FiltroNotas {
  empresaId?: number | null;
  // Nome da situação (ex.: "Lancada") ou SITUACAO_PENDENTES
  situacao?: string;
  de?: string;
  ate?: string;
  // Caixa de busca: NF, fornecedor, empresa ou CNPJ
  busca?: string;
  // Coluna, com "-" na frente para decrescente (ex.: "-valor")
  ordem?: string;
  pagina?: number;
}

/** Jobs em segundo plano que o admin pode rodar na hora. */
export type NomeJob = 'leitura-email' | 'lancamento' | 'sincronizacao';

/** GET /api/jobs/{nome}/situacao: true enquanto o job está na fila ou rodando. */
export interface SituacaoJob {
  nome: string;
  emExecucao: boolean;
}

/** Situações em que alguém precisa resolver a nota; só estas podem ser marcadas como lançadas manualmente. */
export const SITUACOES_COM_PROBLEMA = ['Erro', 'BlingDesconectado', 'EmpresaNaoEncontrada', 'SemParcelas'];

/** Filtro que junta todas as situações que ainda não viraram conta a pagar. */
export const SITUACAO_PENDENTES = 'Pendentes';

export const DESCRICAO_STATUS: { [status: string]: string } = {
  Lancada: 'Lançada no Bling',
  LancadaManualmente: 'Lançada manualmente',
  Recebida: 'Aguardando lançamento',
  SemParcelas: 'Sem vencimentos no XML',
  EmpresaNaoEncontrada: 'CNPJ não cadastrado',
  BlingDesconectado: 'Bling desconectado',
  Erro: 'Erro ao lançar'
};

/** Uma tentativa que falhou ao ler a nota do e-mail ou ao lançá-la no Bling. */
export interface Falha {
  id: number;
  ocorridaEm: string;
  etapa: string;
  mensagem: string;
  // "GET", "POST" ou "IMAP"
  metodo: string;
  // URL chamada no Bling ou servidor de e-mail
  endereco: string;
  statusHttp: number | null;
  resposta: string | null;
  empresaId: number | null;
  empresaNome: string | null;
  notaFiscalId: number | null;
  numeroNota: string | null;
  chaveAcesso: string | null;
  fornecedor: string | null;
  cnpjFornecedor: string | null;
  valorNota: number | null;
  numeroParcela: string | null;
  valorParcela: number | null;
  vencimentoParcela: string | null;
  origem: string | null;
}

export interface PaginaFalhas {
  itens: Falha[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
}

/** Filtros da tela de falhas. Datas no formato yyyy-MM-dd. */
export interface FiltroFalhas {
  empresaId?: number | null;
  notaId?: number | null;
  etapa?: string;
  de?: string;
  ate?: string;
  // Caixa de busca: NF, fornecedor, empresa ou CNPJ
  busca?: string;
  // Coluna, com "-" na frente para decrescente (ex.: "-data")
  ordem?: string;
  pagina?: number;
}

/** Nome de cada etapa como aparece na tela, na ordem do caminho e-mail → Bling. */
export const DESCRICAO_ETAPA: { [etapa: string]: string } = {
  LeituraEmail: 'Leitura do e-mail',
  LeituraXml: 'Leitura do XML',
  RenovacaoToken: 'Renovação do acesso ao Bling',
  BuscaFornecedor: 'Busca do fornecedor no Bling',
  CadastroFornecedor: 'Cadastro do fornecedor no Bling',
  ConferenciaBling: 'Conferência no Bling',
  LancamentoContaPagar: 'Lançamento da conta a pagar'
};

export type TipoAviso = 'NotaComProblema' | 'BlingDesconectado' | 'CaixaEmailFalhando' | 'ContasVencidas';

/** Um problema em aberto que precisa de alguém (sininho do topo). */
export interface Aviso {
  id: number;
  tipo: TipoAviso;
  titulo: string;
  mensagem: string;
  // Rota do front, ex.: "/pages/notas-pendentes"
  link: string;
  clienteId: number;
  clienteNome: string;
  criadoEm: string;
  // Ainda não visto por este usuário
  novo: boolean;
}

export interface PainelAvisos {
  novos: number;
  total: number;
  itens: Aviso[];
}

/** Resumo por e-mail da rede. dia: 0 = domingo ... 6 = sábado; 7 = todo dia útil (seg a sex); hora no horário de Brasília. */
export interface ResumoSemanal {
  ativo: boolean;
  dia: number;
  hora: number;
  ultimoEnvioEm: string | null;
  // Usuários ativos da rede que recebem
  destinatarios: number;
}

export interface AlteracaoResumoSemanal {
  ativo: boolean;
  dia: number;
  hora: number;
}

// ---------- Saúde das integrações (admin) ----------

export interface SaudeCaixa {
  usuario: string;
  ultimaLeituraEm: string | null;
  ultimaFalhaEm: string | null;
  ultimoErro: string | null;
  falhando: boolean;
}

export interface SaudeEmpresa {
  id: number;
  nome: string;
  cnpj: string;
  blingConectado: boolean;
  tokenValidoAte: string | null;
  ultimaSincronizacaoEm: string | null;
  // Só para empresa com caixa de e-mail própria
  caixa: SaudeCaixa | null;
  notasAguardando: number;
  notasComProblema: number;
  avisosAbertos: number;
}

export interface SaudeRede {
  clienteId: number;
  nome: string;
  contrato?: SituacaoContrato;
  diasRestantesTeste?: number | null;
  atencao: boolean;
  avisosAbertos: number;
  caixa: SaudeCaixa | null;
  notasAguardando: number;
  notasComProblema: number;
  ultimaNotaEm: string | null;
  empresas: SaudeEmpresa[];
}

export type SituacaoJobAgendado = 'Ok' | 'Falhou' | 'TentandoDeNovo' | 'Rodando' | 'NuncaRodou';

export interface SaudeJob {
  id: string;
  nome: string;
  situacao: SituacaoJobAgendado;
  ultimaExecucaoEm: string | null;
  proximaExecucaoEm: string | null;
  erro: string | null;
}

export interface Saude {
  geradoEm: string;
  redesComAtencao: number;
  jobs: SaudeJob[];
  redes: SaudeRede[];
}

// ---------- Notas que chegaram só em PDF ----------

export type SituacaoNotaSoPdf = 'BuscandoNaSefaz' | 'SemChave' | 'SemCertificado' | 'NaoResolvida';

export const DESCRICAO_SITUACAO_SO_PDF: Record<SituacaoNotaSoPdf, string> = {
  BuscandoNaSefaz: 'Buscando o XML na SEFAZ',
  SemChave: 'Sem chave de acesso',
  SemCertificado: 'Falta o certificado A1',
  NaoResolvida: 'SEFAZ não devolveu o XML'
};

/** E-mail que trouxe a nota só em PDF, sem o XML. */
export interface NotaSoPdf {
  id: number;
  recebidaEm: string;
  remetente: string;
  nomeRemetente: string;
  assunto: string;
  pdfNome: string;
  empresaCaixaNome: string | null;
  chaveAcesso: string | null;
  // "nome do arquivo", "PDF", "assunto", "corpo do e-mail" ou "informada"
  origemChave: string | null;
  numeroNota: string | null;
  cnpjFornecedor: string | null;
  situacao: SituacaoNotaSoPdf;
  motivo: string | null;
  tentativasSefaz: number;
  proximaTentativaEm: string | null;
  cienciaEnviadaEm: string | null;
  xmlPedidoEm: string | null;
}

export const UFS = ['AC', 'AL', 'AM', 'AP', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MG', 'MS', 'MT', 'PA', 'PB', 'PE', 'PI', 'PR',
  'RJ', 'RN', 'RO', 'RR', 'RS', 'SC', 'SE', 'SP', 'TO'];

/** Texto e cor do selo do contrato (Clientes, Saúde, detalhe do cliente). */
export function seloContrato(contrato: SituacaoContrato | undefined, diasRestantes: number | null | undefined): { texto: string; classe: string } {
  if (contrato === 'EmTeste') {
    if (diasRestantes === 1) {
      return { texto: 'Teste · último dia', classe: 'badge badge-warning' };
    }
    return { texto: 'Teste · faltam ' + (diasRestantes ?? 0) + ' dias', classe: 'badge badge-info' };
  }
  if (contrato === 'TesteEncerrado') {
    return { texto: 'Teste encerrado', classe: 'badge badge-danger' };
  }
  return { texto: 'Contratado', classe: 'badge badge-success' };
}

/** Avisos de vencimento da rede: tolerância das vencidas (dias de atraso) e o e-mail diário de hoje e amanhã. */
export interface AlertasVencimento {
  avisoVencidasDias: number;
  alertaDiarioAtivo: boolean;
  alertaDiarioHora: number;
}

/** Conferência com o Bling da rede: evita lançar de novo a nota lançada à mão e fecha as pendentes encontradas lá. */
export interface ConferenciaBling {
  conferirAntesDeLancar: boolean;
  exigeNumeroNota: boolean;
  /** Diferença de valor aceita, em reais (0 a 5). */
  toleranciaValor: number;
  /** Diferença de vencimento aceita, em dias (0 a 5). */
  toleranciaDias: number;
  fecharPendentes: boolean;
}
