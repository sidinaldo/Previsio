import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { ContaPagar, FiltroContas, PaginaContasPagar } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';
import { ContasPagarComponent } from './contas-pagar.component';

const CONTAS: ContaPagar[] = [
  { id: 1, blingId: 9001, empresaId: 10, empresaNome: 'Auto Center Centro', vencimento: '2026-11-05T00:00:00', valor: 1250,
    situacao: 1, situacaoTexto: 'Em aberto', vencida: true, fornecedor: 'Contabilidade Silva', origem: 'Bling',
    notaFiscalId: null, numeroNota: null, numeroParcela: null,
    formaPagamento: 'Débito em conta', formaGrupo: 'debito-automatico', formaGrupoNome: 'Débito automático', saiSozinho: true },
  { id: 2, blingId: 9002, empresaId: 10, empresaNome: 'Auto Center Centro', vencimento: '2026-11-20T00:00:00', valor: 3240,
    situacao: 1, situacaoTexto: 'Em aberto', vencida: false, fornecedor: 'ALFA DISTRIBUIDORA', origem: 'Previsio',
    notaFiscalId: 5, numeroNota: '48213', numeroParcela: '001' },
  { id: 3, blingId: 9003, empresaId: 11, empresaNome: 'Auto Center Norte', vencimento: '2026-11-25T00:00:00', valor: 80,
    situacao: 1, situacaoTexto: 'Em aberto', vencida: false, fornecedor: null, origem: 'Bling',
    notaFiscalId: null, numeroNota: null, numeroParcela: null }
];

const PAGINA: PaginaContasPagar = {
  itens: CONTAS, total: 3, pagina: 1, tamanhoPagina: 50, valorTotal: 4570,
  sincronizadaEm: '2026-11-11T12:30:00Z', diasPassados: 30, diasFuturos: 90
};

async function montar(parametros: Record<string, string> = {}) {
  const api = {
    obterContasPagar: vi.fn((_: FiltroContas) => of(PAGINA)),
    exportarContasFiltradas: vi.fn(),
    listarEmpresas: vi.fn(() => of([]))
  };
  TestBed.configureTestingModule({
    imports: [ContasPagarComponent],
    providers: [
      provideRouter([]),
      { provide: PrevisioService, useValue: api },
      { provide: SessaoService, useValue: { clienteId: signal(2) } },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(parametros) } } }
    ]
  });
  const fixture = TestBed.createComponent(ContasPagarComponent);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { fixture, api, tela: fixture.nativeElement as HTMLElement };
}

describe('ContasPagarComponent', () => {
  beforeEach(() => localStorage.clear());

  it('abre com as em aberto até daqui a 30 dias e mostra origem, fornecedor e vencida', async () => {
    const { tela, api } = await montar();
    const filtro = api.obterContasPagar.mock.calls[0][0];
    expect(filtro.situacao).toBe('Abertas');
    expect(filtro.de).toBe('');
    expect(filtro.ate).not.toBe('');

    const linhas = tela.querySelectorAll('tbody tr');
    expect(linhas.length).toBe(3);
    expect(linhas[0].textContent).toContain('Contabilidade Silva');
    expect(linhas[0].textContent).toContain('Lançada direto no Bling');
    expect(linhas[0].querySelector('.badge')?.textContent).toContain('Vencida');
    expect(linhas[1].textContent).toContain('NF 48213');
    expect(linhas[1].textContent).toContain('parcela 001');
    expect(linhas[2].textContent).toContain('Fornecedor ainda não identificado');
    expect(tela.textContent).toContain('3 contas');
    expect(tela.textContent).toMatch(/4[.,]570/);
  });

  it('guarda o filtro usado e volta ao padrão no Limpar', async () => {
    const { fixture, api } = await montar();
    const tela = fixture.componentInstance;
    tela.origem.set('Bling');
    tela.situacao.set('Pagas');
    tela.filtrar();
    expect(api.obterContasPagar.mock.calls[1][0].origem).toBe('Bling');
    expect(JSON.parse(localStorage.getItem('previsio.filtro-contas.2')!).situacao).toBe('Pagas');

    tela.limpar();
    const ultimo = api.obterContasPagar.mock.calls[2][0];
    expect(ultimo.situacao).toBe('Abertas');
    expect(ultimo.origem).toBe('');
  });

  it('do painel abre nas em aberto da categoria, no período, e o filtro sai no x', async () => {
    localStorage.setItem('previsio.filtro-contas.2', JSON.stringify({ empresaId: 10, situacao: 'Pagas', origem: 'Bling', de: '', ate: '', datasPadrao: true }));
    const { fixture, api, tela } = await montar({ categoria: 'Fretes', de: '2026-11-13', ate: '2026-12-12' });
    const filtro = api.obterContasPagar.mock.calls[0][0];
    expect(filtro.categoria).toBe('Fretes');
    expect(filtro.situacao).toBe('Abertas');
    expect(filtro.empresaId).toBeNull();
    expect(filtro.origem).toBe('');
    expect(filtro.de).toBe('2026-11-13');
    expect(filtro.ate).toBe('2026-12-12');
    expect(tela.querySelector('.pv-filtro-categoria')?.textContent).toContain('Fretes');

    (tela.querySelector('.pv-filtro-categoria__remover') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.obterContasPagar.mock.calls[1][0].categoria).toBe('');
    expect(tela.querySelector('.pv-filtro-categoria')).toBeNull();
  });

  it('"Por página" pede o tamanho à API, volta para a página 1 e guarda a escolha', async () => {
    const { fixture, api, tela } = await montar();
    const pagina30 = { ...PAGINA, total: 30, tamanhoPagina: 25 };
    api.obterContasPagar.mockReturnValue(of(pagina30));
    fixture.componentInstance.carregar();
    await fixture.whenStable();
    expect(api.obterContasPagar.mock.calls[0][0].tamanho).toBe(25);

    const seletor = tela.querySelector('app-paginacao select') as HTMLSelectElement;
    expect(Array.from(seletor.options).map(o => o.value)).toEqual(['10', '25', '50', '100']);
    seletor.value = '10';
    seletor.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    const ultimo = api.obterContasPagar.mock.calls[api.obterContasPagar.mock.calls.length - 1][0];
    expect(ultimo.tamanho).toBe(10);
    expect(ultimo.pagina).toBe(1);
    expect(localStorage.getItem('previsio.por-pagina.contas-pagar')).toBe('10');
  });

  it('link do e-mail: vencidas de qualquer data, sem empresa nem origem guardadas', async () => {
    localStorage.setItem('previsio.filtro-contas.2', JSON.stringify({ empresaId: 10, situacao: 'Pagas', origem: 'Bling', de: '2026-11-01', ate: '2026-11-30', datasPadrao: false }));
    const { api } = await montar({ situacao: 'Vencidas' });
    const filtro = api.obterContasPagar.mock.calls[0][0];
    expect(filtro.situacao).toBe('Vencidas');
    expect(filtro.de).toBe('');
    expect(filtro.ate).toBe('');
    expect(filtro.empresaId).toBeNull();
    expect(filtro.origem).toBe('');
    expect(filtro.categoria).toBe('');
  });

  it('link do e-mail: as em aberto que vencem no dia', async () => {
    const { api } = await montar({ situacao: 'Abertas', de: '2026-11-14', ate: '2026-11-14' });
    const filtro = api.obterContasPagar.mock.calls[0][0];
    expect(filtro.situacao).toBe('Abertas');
    expect(filtro.de).toBe('2026-11-14');
    expect(filtro.ate).toBe('2026-11-14');
  });

  it('situação inválida no endereço é ignorada e vale o filtro guardado', async () => {
    localStorage.setItem('previsio.filtro-contas.2', JSON.stringify({ empresaId: 10, situacao: 'Pagas', origem: 'Bling', de: '', ate: '', datasPadrao: true }));
    const { api } = await montar({ situacao: 'Qualquer' });
    expect(api.obterContasPagar.mock.calls[0][0].situacao).toBe('Pagas');
  });
  it('mostra a forma de pagamento e filtra pelo que sai sozinho; o link do painel traz a forma', async () => {
    const { fixture, tela, api } = await montar();
    const linhas = tela.querySelectorAll('tbody tr');
    expect(linhas[0].textContent).toContain('Débito em conta');
    expect(linhas[0].textContent).toContain('sai sozinho da conta');
    expect(linhas[1].textContent).not.toContain('sai sozinho');

    fixture.componentInstance.forma.set('sai-sozinho');
    fixture.componentInstance.filtrar();
    expect(api.obterContasPagar.mock.calls[1][0].forma).toBe('sai-sozinho');
    expect(JSON.parse(localStorage.getItem('previsio.filtro-contas.2')!).forma).toBe('sai-sozinho');

    TestBed.resetTestingModule();
    const doPainel = await montar({ forma: 'boleto', situacao: 'Abertas', de: '2026-11-11', ate: '2026-12-11' });
    const filtro = doPainel.api.obterContasPagar.mock.calls[0][0];
    expect(filtro.forma).toBe('boleto');
    expect(filtro.de).toBe('2026-11-11');

    TestBed.resetTestingModule();
    // Forma inválida no endereço: vale o último filtro guardado (o do link do painel)
    const invalida = await montar({ forma: 'cheque' });
    expect(invalida.api.obterContasPagar.mock.calls[0][0].forma).toBe('boleto');
  });
});
