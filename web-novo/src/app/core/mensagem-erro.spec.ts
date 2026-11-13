import { HttpErrorResponse } from '@angular/common/http';

import { mensagemComMotivo, mensagemDaApi } from './mensagem-erro';

describe('mensagens de erro da API', () => {
  it('usa o texto da API quando vem', () => {
    const erro = new HttpErrorResponse({ status: 400, error: 'A nota já foi lançada.' });
    expect(mensagemDaApi(erro, 'padrão')).toBe('A nota já foi lançada.');
    expect(mensagemComMotivo(erro, 'Não foi possível reprocessar a NF 93004'))
      .toBe('Não foi possível reprocessar a NF 93004: A nota já foi lançada.');
  });

  it('sem texto mostra o código do erro ou que a API não respondeu', () => {
    expect(mensagemComMotivo(new HttpErrorResponse({ status: 500 }), 'Não foi possível reprocessar a NF 1'))
      .toBe('Não foi possível reprocessar a NF 1 (erro 500).');
    expect(mensagemComMotivo(new HttpErrorResponse({ status: 0 }), 'Não foi possível reprocessar a NF 1'))
      .toBe('Não foi possível reprocessar a NF 1: a API não respondeu.');
    expect(mensagemComMotivo(new Error('x'), 'Falhou')).toBe('Falhou.');
  });
});
