import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

import { PREVISIO_CONFIG } from './previsio.config';
import { SessaoService } from './sessao.service';

const URL_LOGIN = `${PREVISIO_CONFIG.apiUrl}/auth/login`;

/**
 * Envia o token JWT nas chamadas à API do Previsio.
 * Se a API responder 401 (token vencido, senha trocada, usuário ou cliente desativado),
 * encerra o login e leva para a tela de entrada.
 */
export const autenticacaoInterceptor: HttpInterceptorFn = (requisicao, proximo) => {
  // Outros endereços (fontes, arquivos) seguem sem o token
  if (!requisicao.url.startsWith(PREVISIO_CONFIG.apiUrl + '/')) {
    return proximo(requisicao);
  }

  // No login, um 401 é só "senha errada": quem trata é a própria tela
  if (requisicao.url === URL_LOGIN) {
    return proximo(requisicao);
  }

  const sessao = inject(SessaoService);
  let enviada = requisicao;
  const token = sessao.token();
  if (token) {
    enviada = requisicao.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }

  return proximo(enviada).pipe(
    catchError((erro: unknown) => {
      if (erro instanceof HttpErrorResponse && erro.status === 401) {
        sessao.sair('expirou');
      }
      return throwError(() => erro);
    })
  );
};
