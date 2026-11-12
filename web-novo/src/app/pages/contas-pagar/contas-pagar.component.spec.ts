import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { ContaPagar, FiltroContas, PaginaContasPagar } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';
import { ContasPagarComponent } from './contas-pagar.component';

const CONTAS: ContaPagar[] = [
  { id: 1, blingId: 9001, empresaId: 10, empresaNome: 'Auto Center Centro', vencimento: '2026-11-05T00:00:00', valor: 1250,
    situacao: 1, situacaoTexto: 'Em aberto', vencida: true, fornecedor: 'Contabilidade Silva', origem: 'Bling',
    notaFiscalId: null, numeroNota: null, numeroParcela: null },
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

async function montar() {
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
      { provide: SessaoService, useValue: { clienteId: signal(2) } }
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
});
