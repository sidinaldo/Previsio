import { HttpParams } from '@angular/common/http';

import { FiltroContas } from './previsio.models';

/** O que o badge da situação usa: vale para conta a pagar e a receber. */
export interface SituacaoDaConta {
  vencida: boolean;
  situacao: number;
  situacaoTexto: string;
}

// Ao abrir a tela: as em aberto (inclusive as vencidas) que vencem até daqui a N dias
export const DIAS_PADRAO_CONTAS = 30;

const CHAVE_FILTRO = 'previsio.filtro-contas.';

/**
 * Monta os parâmetros de GET /api/clientes/{id}/contas-pagar, deixando de fora os filtros vazios.
 */
export function parametrosDasContas(filtro: FiltroContas): HttpParams {
  let params = new HttpParams();
  if (filtro.empresaId) {
    params = params.set('empresaId', String(filtro.empresaId));
  }
  if (filtro.situacao) {
    params = params.set('situacao', filtro.situacao);
  }
  if (filtro.origem) {
    params = params.set('origem', filtro.origem);
  }
  if (filtro.de) {
    params = params.set('de', filtro.de);
  }
  if (filtro.ate) {
    params = params.set('ate', filtro.ate);
  }
  if (filtro.busca && filtro.busca.trim() !== '') {
    params = params.set('busca', filtro.busca.trim());
  }
  if (filtro.categoria) {
    params = params.set('categoria', filtro.categoria);
  }
  if (filtro.forma) {
    params = params.set('forma', filtro.forma);
  }
  if (filtro.ordem) {
    params = params.set('ordem', filtro.ordem);
  }
  if (filtro.pagina && filtro.pagina > 1) {
    params = params.set('pagina', String(filtro.pagina));
  }
  if (filtro.tamanho) {
    params = params.set('tamanho', String(filtro.tamanho));
  }
  return params;
}

/** Data local no formato yyyy-MM-dd (o do input date). */
export function dataIso(data: Date): string {
  const mes = String(data.getMonth() + 1).padStart(2, '0');
  const dia = String(data.getDate()).padStart(2, '0');
  return `${data.getFullYear()}-${mes}-${dia}`;
}

/** Filtro ao abrir a tela: em aberto (as vencidas entram), vencendo até hoje + 30 dias. */
export function filtroPadraoContas(hoje: Date): FiltroContas {
  const ate = new Date(hoje.getFullYear(), hoje.getMonth(), hoje.getDate() + DIAS_PADRAO_CONTAS);
  return { empresaId: null, situacao: 'Abertas', origem: '', forma: '', de: '', ate: dataIso(ate), busca: '' };
}

/** O que fica guardado no navegador. Com datasPadrao, o período é recalculado a cada dia. */
interface FiltroSalvo {
  empresaId: number | null;
  situacao: string;
  origem: string;
  // Guardado a partir da forma de pagamento; filtro antigo não tem
  forma?: string;
  de: string;
  ate: string;
  datasPadrao: boolean;
}

/** Guarda o último filtro usado (por cliente). Sem armazenamento no navegador, não faz nada. */
export function salvarFiltroContas(clienteId: number | null, filtro: FiltroContas, hoje: Date): void {
  if (!clienteId) {
    return;
  }
  const padrao = filtroPadraoContas(hoje);
  const salvo: FiltroSalvo = {
    empresaId: filtro.empresaId ?? null,
    situacao: filtro.situacao ?? '',
    origem: filtro.origem ?? '',
    forma: filtro.forma ?? '',
    de: filtro.de ?? '',
    ate: filtro.ate ?? '',
    datasPadrao: (filtro.de ?? '') === padrao.de && (filtro.ate ?? '') === padrao.ate
  };
  try {
    localStorage.setItem(CHAVE_FILTRO + clienteId, JSON.stringify(salvo));
  } catch {
    // Navegador sem armazenamento: o filtro vale só até sair da tela
  }
}

/** Último filtro usado neste navegador ou, sem ele, o padrão. A busca não fica guardada. */
export function lerFiltroContas(clienteId: number | null, hoje: Date): FiltroContas {
  const padrao = filtroPadraoContas(hoje);
  if (!clienteId) {
    return padrao;
  }
  let texto: string | null = null;
  try {
    texto = localStorage.getItem(CHAVE_FILTRO + clienteId);
  } catch {
    texto = null;
  }
  if (!texto) {
    return padrao;
  }
  try {
    const salvo = JSON.parse(texto) as FiltroSalvo;
    const filtro: FiltroContas = {
      empresaId: typeof salvo.empresaId === 'number' ? salvo.empresaId : null,
      situacao: typeof salvo.situacao === 'string' ? salvo.situacao : padrao.situacao,
      origem: typeof salvo.origem === 'string' ? salvo.origem : '',
      forma: typeof salvo.forma === 'string' ? salvo.forma : '',
      de: typeof salvo.de === 'string' ? salvo.de : '',
      ate: typeof salvo.ate === 'string' ? salvo.ate : '',
      busca: ''
    };
    if (salvo.datasPadrao) {
      filtro.de = padrao.de;
      filtro.ate = padrao.ate;
    }
    return filtro;
  } catch {
    return padrao;
  }
}

/** Opções do filtro de forma de pagamento (os valores são os grupos da API). */
export const FORMAS_FILTRO: { valor: string; texto: string }[] = [
  { valor: '', texto: 'Todas' },
  { valor: 'precisa-pagar', texto: 'Precisam ser pagas' },
  { valor: 'sai-sozinho', texto: 'Saem sozinhas da conta' },
  { valor: 'boleto', texto: 'Boleto' },
  { valor: 'pix', texto: 'Pix' },
  { valor: 'transferencia', texto: 'Transferência/depósito' },
  { valor: 'cartao', texto: 'Cartão' },
  { valor: 'dinheiro', texto: 'Dinheiro' },
  { valor: 'debito-automatico', texto: 'Débito automático' },
  { valor: 'outras', texto: 'Outras' },
  { valor: 'sem', texto: 'Sem forma no Bling' }
];

/** Cor do badge da situação da conta. */
export function classeDaConta(conta: SituacaoDaConta): string {
  if (conta.vencida) {
    return 'badge-danger';
  }
  if (conta.situacao === 2) {
    return 'badge-success';
  }
  if (conta.situacao === 3) {
    return 'badge-info';
  }
  if (conta.situacao === 4 || conta.situacao === 5) {
    return 'badge-default';
  }
  return 'badge-warning';
}

/** Texto da situação; em aberto e já vencida aparece como "Vencida". */
export function textoDaConta(conta: SituacaoDaConta): string {
  if (conta.vencida) {
    if (conta.situacao === 3) {
      return 'Vencida (parcial)';
    }
    return 'Vencida';
  }
  return conta.situacaoTexto;
}
