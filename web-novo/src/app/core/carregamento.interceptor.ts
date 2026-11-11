import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { finalize } from 'rxjs';

import { CarregamentoService } from './carregamento.service';
import { PREVISIO_CONFIG } from './previsio.config';

/**
 * Conta as chamadas à API em andamento, para a barra de carregamento do topo.
 * Termina a contagem com sucesso, erro ou cancelamento.
 */
export const carregamentoInterceptor: HttpInterceptorFn = (requisicao, proximo) => {
  if (!requisicao.url.startsWith(PREVISIO_CONFIG.apiUrl + '/')) {
    return proximo(requisicao);
  }

  const carregamento = inject(CarregamentoService);
  carregamento.iniciar();
  return proximo(requisicao).pipe(finalize(() => carregamento.terminar()));
};
