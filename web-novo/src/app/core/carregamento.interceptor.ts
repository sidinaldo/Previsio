import { inject } from '@angular/core';
import { HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import { finalize } from 'rxjs';

import { CarregamentoService } from './carregamento.service';
import { PREVISIO_CONFIG } from './previsio.config';

/** Marca chamadas feitas sozinhas, em segundo plano (ex.: o sininho), que não mostram a barra do topo. */
export const SEM_BARRA_CARREGAMENTO = new HttpContextToken<boolean>(() => false);

/**
 * Conta as chamadas à API em andamento, para a barra de carregamento do topo.
 * Termina a contagem com sucesso, erro ou cancelamento.
 */
export const carregamentoInterceptor: HttpInterceptorFn = (requisicao, proximo) => {
  if (!requisicao.url.startsWith(PREVISIO_CONFIG.apiUrl + '/') || requisicao.context.get(SEM_BARRA_CARREGAMENTO)) {
    return proximo(requisicao);
  }

  const carregamento = inject(CarregamentoService);
  carregamento.iniciar();
  return proximo(requisicao).pipe(finalize(() => carregamento.terminar()));
};
