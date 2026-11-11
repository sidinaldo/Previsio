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
}

export interface Empresa {
  id: number;
  nome: string;
  cnpj: string;
  blingConectado: boolean;
  /** Aplicativo privado criado na conta Bling desta empresa; null usa o aplicativo padrão do Previsio. */
  blingClientId: string | null;
  /** O secret nunca volta da API, só se está cadastrado. */
  blingClientSecretCadastrado: boolean;
  ultimaSincronizacaoEm: string | null;
  ativo: boolean;
}

/** POST /api/clientes/{id}/empresas. Client ID e Secret são opcionais, mas vão juntos. */
export interface NovaEmpresa {
  nome: string;
  cnpj: string;
  blingClientId?: string;
  blingClientSecret?: string;
}

/** PUT /api/empresas/{id}: só os campos enviados mudam. O CNPJ não pode ser alterado. */
export interface AlteracaoEmpresa {
  nome?: string;
  ativo?: boolean;
  /** "" volta ao aplicativo padrão do Previsio. Trocar o Client ID desconecta a empresa. */
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
  ativo: boolean;
}

/** PUT /api/clientes/{id}: só os campos enviados mudam; a senha só troca se vier preenchida. */
export interface AlteracaoCliente {
  nome?: string;
  imapHost?: string;
  imapPorta?: number;
  imapSsl?: boolean;
  imapUsuario?: string;
  imapSenha?: string;
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
export interface NotaRecebida {
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
}

export interface PaginaNotas {
  itens: NotaRecebida[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
}

/** Filtros da tela de notas recebidas. Datas (dia em que a nota chegou) no formato yyyy-MM-dd. */
export interface FiltroNotas {
  empresaId?: number | null;
  // Nome da situação (ex.: "Lancada") ou SITUACAO_PENDENTES
  situacao?: string;
  de?: string;
  ate?: string;
  pagina?: number;
}

/** Jobs em segundo plano que o admin pode rodar na hora. */
export type NomeJob = 'leitura-email' | 'lancamento' | 'sincronizacao';

/** GET /api/jobs/{nome}/situacao: true enquanto o job está na fila ou rodando. */
export interface SituacaoJob {
  nome: string;
  emExecucao: boolean;
}

/** Filtro que junta todas as situações que ainda não viraram conta a pagar. */
export const SITUACAO_PENDENTES = 'Pendentes';

export const DESCRICAO_STATUS: { [status: string]: string } = {
  Lancada: 'Lançada no Bling',
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
  pagina?: number;
}

/** Nome de cada etapa como aparece na tela, na ordem do caminho e-mail → Bling. */
export const DESCRICAO_ETAPA: { [etapa: string]: string } = {
  LeituraEmail: 'Leitura do e-mail',
  LeituraXml: 'Leitura do XML',
  RenovacaoToken: 'Renovação do acesso ao Bling',
  BuscaFornecedor: 'Busca do fornecedor no Bling',
  CadastroFornecedor: 'Cadastro do fornecedor no Bling',
  LancamentoContaPagar: 'Lançamento da conta a pagar'
};
