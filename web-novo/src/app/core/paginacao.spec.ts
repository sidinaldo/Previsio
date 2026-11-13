import { TAMANHO_PAGINA_API_PADRAO, lerTamanhoPagina, paginar, salvarTamanhoPagina } from './paginacao';

describe('paginação', () => {
  beforeEach(() => localStorage.clear());

  it('"Por página" das listas da API: 25 sem escolha, guarda por tela e ignora valor estranho', () => {
    expect(lerTamanhoPagina('contas-pagar')).toBe(TAMANHO_PAGINA_API_PADRAO);
    salvarTamanhoPagina('contas-pagar', 100);
    expect(lerTamanhoPagina('contas-pagar')).toBe(100);
    expect(lerTamanhoPagina('falhas')).toBe(25);
    localStorage.setItem('previsio.por-pagina.falhas', '7');
    expect(lerTamanhoPagina('falhas')).toBe(25);
  });

  it('paginar devolve os itens da página e cai na última se a lista encolheu', () => {
    const lista = Array.from({ length: 23 }, (_, i) => i + 1);
    expect(paginar(lista, 3, 10)).toEqual([21, 22, 23]);
    expect(paginar(lista, 9, 10)).toEqual([21, 22, 23]);
  });
});
