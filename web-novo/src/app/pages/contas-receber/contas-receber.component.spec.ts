import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { ContaReceber, FiltroContas, PaginaContasReceber } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { ContasReceberComponent } from './contas-receber.component';

const CONTAS: ContaReceber[] = [
  { id: 1, blingId: 7001, empresaId: 10, empresaNome: 'Auto Center Centro', vencimento: '2026-11-05T00:00:00', valor: 300,
    situacao: 1, situacaoTexto: 'Em aberto', vencida: true, cliente: 'Oficina do Zé' },
  { id: 2, blingId: 7002, empresaId: 11, empresaNome: 'Auto Center Norte', vencimento: '2026-11-20T00:00:00', valor: 900,
    situacao: 2, situacaoTexto: 'Recebida', vencida: false, cliente: null }
];

function pagina(fluxoCaixaAtivo: boolean): PaginaContasReceber {
  return { itens: CONTAS, total: 2, pagina: 1, tamanhoPagina: 50, valorTotal: 1200,
    sincronizadaEm: null, diasPassados: 30, diasFuturos: 90, fluxoCaixaAtivo };
}

async function montar(situacao: string | null, fluxoCaixaAtivo = true) {
  const api = {
    obterContasReceber: vi.fn((_: FiltroContas) => of(pagina(fluxoCaixaAtivo))),
    exportarContasReceber: vi.fn(),
    listarEmpresas: vi.fn(() => of([]))
  };
  const parametros: Record<string, string> = {};
  if (situacao) {
    parametros['situacao'] = situacao;
  }
  TestBed.configureTestingModule({
    imports: [ContasReceberComponent],
    providers: [
      provideRouter([]),
      { provide: PrevisioService, useValue: api },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(parametros) } } }
    ]
  });
  const fixture = TestBed.createComponent(ContasReceberComponent);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { fixture, api, tela: fixture.nativeElement as HTMLElement };
}

describe('ContasReceberComponent', () => {
  it('abre com as em aberto até daqui a 30 dias e mostra cliente e situação', async () => {
    const { tela, api } = await montar(null);
    const filtro = api.obterContasReceber.mock.calls[0][0];
    expect(filtro.situacao).toBe('Abertas');
    expect(filtro.ate).not.toBe('');

    const linhas = tela.querySelectorAll('tbody tr');
    expect(linhas.length).toBe(2);
    expect(linhas[0].textContent).toContain('Oficina do Zé');
    expect(linhas[0].querySelector('.badge')?.textContent).toContain('Vencida');
    expect(linhas[1].textContent).toContain('Cliente ainda não identificado');
    expect(linhas[1].querySelector('.badge')?.textContent).toContain('Recebida');
    expect(tela.querySelector('.alert-warning')).toBeNull();
  });

  it('vindo do painel com ?situacao=Vencidas, lista todas as vencidas sem limite de data', async () => {
    const { api } = await montar('Vencidas');
    const filtro = api.obterContasReceber.mock.calls[0][0];
    expect(filtro.situacao).toBe('Vencidas');
    expect(filtro.ate).toBe('');
  });

  it('avisa quando o fluxo de caixa está desligado na rede', async () => {
    const { tela } = await montar(null, false);
    expect(tela.querySelector('.alert-warning')?.textContent).toContain('desligado');
  });
});
