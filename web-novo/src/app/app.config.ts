import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withHashLocation } from '@angular/router';
import localePt from '@angular/common/locales/pt';

import { routes } from './app.routes';
import { autenticacaoInterceptor } from './core/autenticacao.interceptor';

registerLocaleData(localePt, 'pt-BR');

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Rotas com "#" como no template: a API redireciona o OAuth do Bling para /#/pages/empresas
    provideRouter(routes, withHashLocation()),
    // Coloca o token JWT nas chamadas à API e trata o login vencido
    provideHttpClient(withInterceptors([autenticacaoInterceptor])),
    { provide: LOCALE_ID, useValue: 'pt-BR' }
  ]
};
