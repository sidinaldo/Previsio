import { ContaPagar } from './previsio.models';
import { classeDaConta, filtroPadraoContas, lerFiltroContas, parametrosDasContas, salvarFiltroContas, textoDaConta } from './filtro-contas';

function conta(situacao: number, vencida: boolean): ContaPagar {
  return {
    id: 1, blingId: 10, empresaId: 1, empresaNome: 'Loja', vencimento: '2026-11-20', valor: 10, situacao: situacao,
    situacaoTexto: situacao === 2 ? 'Paga' : 'Em aberto', vencida: vencida, fornecedor: null, origem: 'Bling',
    notaFiscalId: null, numeroNota: null, numeroParcela: null
  };
}

describe('filtro de contas a pagar', () => {
  beforeEach(() => localStorage.clear());

  it('não envia filtros vazios e envia os preenchidos', () => {
    expect(parametrosDasContas({ empresaId: null, situacao: '', origem: '', de: '', ate: '', busca: '  ', pagina: 1 }).keys()).toEqual([]);
    const params = parametrosDasContas({ empresaId: 3, situacao: 'Vencidas', origem: 'Bling', de: '2026-11-01', ate: '2026-11-30', busca: ' silva ', ordem: '-valor', pagina: 2, forma: 'sai-sozinho' });
    expect(params.get('empresaId')).toBe('3');
    expect(params.get('situacao')).toBe('Vencidas');
    expect(params.get('origem')).toBe('Bling');
    expect(params.get('de')).toBe('2026-11-01');
    expect(params.get('ate')).toBe('2026-11-30');
    expect(params.get('busca')).toBe('silva');
    expect(params.get('ordem')).toBe('-valor');
    expect(params.get('pagina')).toBe('2');
    expect(params.get('forma')).toBe('sai-sozinho');
  });

  it('padrão: em aberto, vencendo até hoje + 30 dias, sem data inicial', () => {
    expect(filtroPadraoContas(new Date(2026, 10, 11))).toEqual(
      { empresaId: null, situacao: 'Abertas', origem: '', forma: '', de: '', ate: '2026-12-11', busca: '' });
  });

  it('guarda o último filtro por cliente; o período padrão anda com o dia e a busca não fica', () => {
    const hoje = new Date(2026, 10, 11);
    salvarFiltroContas(2, { ...filtroPadraoContas(hoje), empresaId: 7, origem: 'Bling', forma: 'boleto', busca: 'x' }, hoje);
    const amanha = lerFiltroContas(2, new Date(2026, 10, 12));
    expect(amanha.empresaId).toBe(7);
    expect(amanha.forma).toBe('boleto');
    expect(amanha.origem).toBe('Bling');
    expect(amanha.ate).toBe('2026-12-12');
    expect(amanha.busca).toBe('');

    salvarFiltroContas(2, { situacao: 'Pagas', de: '2026-10-01', ate: '2026-10-31' }, hoje);
    const escolhido = lerFiltroContas(2, new Date(2026, 10, 20));
    expect(escolhido.situacao).toBe('Pagas');
    expect(escolhido.de).toBe('2026-10-01');
    expect(escolhido.ate).toBe('2026-10-31');

    // Filtro guardado antes da forma de pagamento
    localStorage.setItem('previsio.filtro-contas.3', JSON.stringify({ empresaId: null, situacao: 'Todas', origem: '', de: '', ate: '', datasPadrao: false }));
    expect(lerFiltroContas(3, hoje).forma).toBe('');

    expect(lerFiltroContas(5, hoje)).toEqual(filtroPadraoContas(hoje));
    localStorage.setItem('previsio.filtro-contas.2', '{quebrado');
    expect(lerFiltroContas(2, hoje)).toEqual(filtroPadraoContas(hoje));
  });

  it('descreve e colore a situação da conta', () => {
    expect(textoDaConta(conta(1, true))).toBe('Vencida');
    expect(classeDaConta(conta(1, true))).toBe('badge-danger');
    expect(textoDaConta(conta(3, true))).toBe('Vencida (parcial)');
    expect(textoDaConta(conta(2, false))).toBe('Paga');
    expect(classeDaConta(conta(2, false))).toBe('badge-success');
    expect(classeDaConta(conta(1, false))).toBe('badge-warning');
    expect(classeDaConta(conta(5, false))).toBe('badge-default');
  });
});
