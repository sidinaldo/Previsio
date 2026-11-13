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

/**
 * "Não foi possível X: <motivo da API>". Sem texto da API, mostra o código do erro
 * (500 = erro interno; 0 = API fora do ar), para dar pista do que houve.
 */
export function mensagemComMotivo(erro: unknown, frase: string): string {
  const motivo = mensagemDaApi(erro, '');
  if (motivo !== '') {
    return frase + ': ' + motivo;
  }
  if (erro instanceof HttpErrorResponse) {
    if (erro.status === 0) {
      return frase + ': a API não respondeu.';
    }
    return frase + ' (erro ' + erro.status + ').';
  }
  return frase + '.';
}
