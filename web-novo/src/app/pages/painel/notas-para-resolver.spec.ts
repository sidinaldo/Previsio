import { urgenciaDaNota } from './notas-para-resolver';

describe('notas para resolver no painel', () => {
  const inicio = '2026-11-12T00:00:00';
  const fim = '2026-11-18T00:00:00';

  it('já venceu', () => {
    expect(urgenciaDaNota('2026-11-11T00:00:00', inicio, fim)).toBe('vencida');
  });

  it('vence dentro do período, inclusive hoje e o último dia', () => {
    expect(urgenciaDaNota('2026-11-12T00:00:00', inicio, fim)).toBe('no-periodo');
    expect(urgenciaDaNota('2026-11-18T00:00:00', inicio, fim)).toBe('no-periodo');
  });

  it('vence depois do período', () => {
    expect(urgenciaDaNota('2026-11-19T00:00:00', inicio, fim)).toBe('depois');
  });

  it('sem parcelas no arquivo da nota', () => {
    expect(urgenciaDaNota(null, inicio, fim)).toBe('sem-vencimento');
  });
});
