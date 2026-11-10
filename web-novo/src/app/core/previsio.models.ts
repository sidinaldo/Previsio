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
  ultimaSincronizacaoEm: string | null;
}

export const DESCRICAO_STATUS: { [status: string]: string } = {
  Recebida: 'Aguardando lançamento',
  SemParcelas: 'Sem vencimentos no XML',
  EmpresaNaoEncontrada: 'CNPJ não cadastrado',
  BlingDesconectado: 'Bling desconectado',
  Erro: 'Erro ao lançar'
};
