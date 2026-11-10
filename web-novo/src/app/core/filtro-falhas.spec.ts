import { parametrosDasFalhas, totalDePaginas } from './filtro-falhas';

describe('filtro de falhas', () => {
  it('não envia filtros vazios', () => {
    const params = parametrosDasFalhas({ empresaId: null, notaId: null, etapa: '', de: '', ate: '', pagina: 1 });
    expect(params.keys()).toEqual([]);
  });

  it('envia os filtros preenchidos', () => {
    const params = parametrosDasFalhas({
      empresaId: 3,
      notaId: 15,
      etapa: 'LancamentoContaPagar',
      de: '2026-11-01',
      ate: '2026-11-10',
      pagina: 2
    });
    expect(params.get('empresaId')).toBe('3');
    expect(params.get('notaId')).toBe('15');
    expect(params.get('etapa')).toBe('LancamentoContaPagar');
    expect(params.get('de')).toBe('2026-11-01');
    expect(params.get('ate')).toBe('2026-11-10');
    expect(params.get('pagina')).toBe('2');
  });

  it('calcula o total de páginas', () => {
    expect(totalDePaginas(0, 50)).toBe(1);
    expect(totalDePaginas(50, 50)).toBe(1);
    expect(totalDePaginas(51, 50)).toBe(2);
  });
});
