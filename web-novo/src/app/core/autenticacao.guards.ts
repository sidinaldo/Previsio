import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { SessaoService } from './sessao.service';

/**
 * Telas internas: sem login (ou com o token vencido) vai para a tela de entrada,
 * guardando a tela pedida para voltar a ela depois de entrar.
 */
export const exigirLogin: CanActivateFn = (_rota, estado) => {
  if (inject(SessaoService).estaLogado()) {
    return true;
  }
  return inject(Router).createUrlTree(['/pages/login'], { queryParams: { retorno: estado.url } });
};

/** Telas só do admin (Clientes): os demais voltam ao painel. */
export const somenteAdmin: CanActivateFn = () => {
  if (inject(SessaoService).ehAdmin()) {
    return true;
  }
  return inject(Router).createUrlTree(['/pages/painel']);
};

/** Tela de login: quem já entrou vai direto para o painel. */
export const somenteSemLogin: CanActivateFn = () => {
  if (inject(SessaoService).estaLogado()) {
    return inject(Router).createUrlTree(['/pages/painel']);
  }
  return true;
};
