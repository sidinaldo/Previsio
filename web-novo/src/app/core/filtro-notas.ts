import { HttpParams } from '@angular/common/http';

import { DESCRICAO_STATUS, FiltroNotas } from './previsio.models';

/**
 * Monta os parâmetros de GET /api/clientes/{id}/notas, deixando de fora os filtros vazios.
 */
export function parametrosDasNotas(filtro: FiltroNotas): HttpParams {
  let params = new HttpParams();

  if (filtro.empresaId) {
    params = params.set('empresaId', String(filtro.empresaId));
  }
  if (filtro.situacao) {
    params = params.set('situacao', filtro.situacao);
  }
  if (filtro.de) {
    params = params.set('de', filtro.de);
  }
  if (filtro.ate) {
    params = params.set('ate', filtro.ate);
  }
  if (filtro.pagina && filtro.pagina > 1) {
    params = params.set('pagina', String(filtro.pagina));
  }
  return params;
}

/** Texto da situação da nota como aparece na tela. */
export function descricaoDaSituacao(status: string): string {
  const texto = DESCRICAO_STATUS[status];
  if (texto) {
    return texto;
  }
  return status;
}

/**
 * Cor do badge da situação: lançada em verde, erro em vermelho, à espera do lançamento em azul,
 * o resto (precisa de alguém resolver) em alerta.
 */
export function classeDaSituacao(status: string): string {
  if (status === 'Lancada') {
    return 'badge-success';
  }
  if (status === 'Erro') {
    return 'badge-danger';
  }
  if (status === 'Recebida') {
    return 'badge-info';
  }
  return 'badge-warning';
}
