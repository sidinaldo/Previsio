import { HttpErrorResponse } from '@angular/common/http';

/**
 * Texto que a API devolve nos erros de regra (BadRequest, Conflict, NotFound),
 * para mostrar ao usuário. Sem texto (API fora do ar, erro interno), usa a mensagem padrão.
 */
export function mensagemDaApi(erro: unknown, padrao: string): string {
  if (erro instanceof HttpErrorResponse && typeof erro.error === 'string' && erro.error.trim() !== '') {
    return erro.error;
  }
  return padrao;
}
