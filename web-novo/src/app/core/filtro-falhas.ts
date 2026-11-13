import { HttpParams } from '@angular/common/http';

import { FiltroFalhas } from './previsio.models';

/**
 * Monta os parâmetros de GET /api/clientes/{id}/falhas, deixando de fora os filtros vazios.
 */
export function parametrosDasFalhas(filtro: FiltroFalhas): HttpParams {
  let params = new HttpParams();

  if (filtro.empresaId) {
    params = params.set('empresaId', String(filtro.empresaId));
  }
  if (filtro.notaId) {
    params = params.set('notaId', String(filtro.notaId));
  }
  if (filtro.etapa) {
    params = params.set('etapa', filtro.etapa);
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

/** Total de páginas para a quantidade de falhas encontradas (no mínimo 1). */
export function totalDePaginas(total: number, tamanhoPagina: number): number {
  if (total <= 0 || tamanhoPagina <= 0) {
    return 1;
  }
  return Math.ceil(total / tamanhoPagina);
}
