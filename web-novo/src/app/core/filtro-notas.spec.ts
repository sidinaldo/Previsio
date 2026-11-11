import { classeDaSituacao, descricaoDaSituacao, parametrosDasNotas } from './filtro-notas';

describe('filtro de notas recebidas', () => {
  it('não envia filtros vazios', () => {
    const params = parametrosDasNotas({ empresaId: null, situacao: '', de: '', ate: '', pagina: 1 });
    expect(params.keys()).toEqual([]);
  });

  it('envia os filtros preenchidos', () => {
    const params = parametrosDasNotas({ empresaId: 2, situacao: 'Pendentes', de: '2026-11-01', ate: '2026-11-10', pagina: 3 });
    expect(params.get('empresaId')).toBe('2');
    expect(params.get('situacao')).toBe('Pendentes');
    expect(params.get('de')).toBe('2026-11-01');
    expect(params.get('ate')).toBe('2026-11-10');
    expect(params.get('pagina')).toBe('3');
  });

  it('descreve e colore a situação', () => {
    expect(descricaoDaSituacao('Lancada')).toBe('Lançada no Bling');
    expect(descricaoDaSituacao('Desconhecida')).toBe('Desconhecida');
    expect(classeDaSituacao('Lancada')).toBe('badge-success');
    expect(classeDaSituacao('Erro')).toBe('badge-danger');
    expect(classeDaSituacao('Recebida')).toBe('badge-info');
    expect(classeDaSituacao('EmpresaNaoEncontrada')).toBe('badge-warning');
  });
});
