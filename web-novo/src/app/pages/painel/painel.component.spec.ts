import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { PainelComponent } from './painel.component';
import { ResumoPainel } from '../../core/previsio.models';
import { SessaoService } from '../../core/sessao.service';

function resumo(valores: number[], notasPendentes: number): ResumoPainel {
  let total = 0;
  for (const valor of valores) {
    total += valor;
  }
  return {
    inicio: '2026-11-10T00:00:00',
    fim: '2026-12-09T00:00:00',
    totalPeriodo: total,
    quantidadeContas: total > 0 ? 3 : 0,
    totalSemanaAtual: valores[0],
    notasPendentes: notasPendentes,
    valorNotasPendentes: notasPendentes * 100,
    semanas: valores.map((valor, i) => ({ inicio: `2026-11-${10 + i * 7}T00:00:00`, fim: `2026-11-${16 + i * 7}T00:00:00`, valor })),
    empresas: []
  };
}

async function montar(dados: ResumoPainel) {
  // O gráfico usa o Chartist global (angular.json); aqui só registra o que foi desenhado
  const desenhos: unknown[] = [];
  (globalThis as any).Chartist = { Bar: function (seletor: string) { desenhos.push(seletor); return { on: () => undefined }; } };

  TestBed.configureTestingModule({
    imports: [PainelComponent],
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
  });
  const sessao = TestBed.inject(SessaoService);
  sessao.escolherCliente(1);
  const fixture = TestBed.createComponent(PainelComponent);
  fixture.autoDetectChanges();
  TestBed.inject(HttpTestingController).expectOne(r => r.url.endsWith('/painel')).flush(dados);
  await fixture.whenStable();
  return { tela: fixture.nativeElement as HTMLElement, desenhos };
}

describe('PainelComponent (visual)', () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('previsio.sessao', JSON.stringify({
      token: 't', expiraEm: '2099-01-01T00:00:00Z',
      usuario: { id: 1, clienteId: null, nome: 'Admin', email: 'a@b.c', perfil: 'Admin', ativo: true, ultimoAcessoEm: null }
    }));
  });

  it('com dados: 4 indicadores, gráfico e resumo semanal', async () => {
    const { tela, desenhos } = await montar(resumo([1000, 2500, 0], 0));
    expect(tela.querySelectorAll('.pv-kpi').length).toBe(4);
    expect(tela.querySelector('#previsioSemanasChart')).not.toBeNull();
    expect(tela.querySelectorAll('.pv-semanas li').length).toBe(3);
    // Semana mais pesada (17/11) destacada no resumo
    expect(tela.querySelector('.pv-semanas__item--maior')!.textContent).toContain('17/11');
    expect(desenhos.length).toBe(1);
  });

  it('tudo zerado: estado vazio com "Ir para Empresas" no lugar dos zeros', async () => {
    const { tela, desenhos } = await montar(resumo([0, 0, 0], 0));
    expect(tela.querySelector('.pv-kpi')).toBeNull();
    expect(tela.querySelector('table')).toBeNull();
    const vazio = tela.querySelector('.pv-vazio') as HTMLElement;
    expect(vazio.textContent).toContain('Nenhuma conta a pagar prevista.');
    expect(vazio.querySelector('a')!.textContent!.trim()).toBe('Ir para Empresas');
    expect(desenhos.length).toBe(0);
  });

  it('só notas fora do Bling: indicadores aparecem e o gráfico dá lugar ao aviso', async () => {
    const { tela, desenhos } = await montar(resumo([0, 0, 0], 2));
    expect(tela.querySelectorAll('.pv-kpi').length).toBe(4);
    expect(tela.querySelector('#previsioSemanasChart')).toBeNull();
    expect(tela.textContent).toContain('Nenhuma saída prevista no período.');
    expect(desenhos.length).toBe(0);
  });

  it('saídas por categoria: barras, link para Contas a pagar e Outras sem link', async () => {
    const dados = resumo([1000, 2500, 0], 0);
    dados.categorias = [
      { nome: 'Compras', tipo: 'categoria', valor: 2000, quantidade: 2 },
      { nome: 'Outras (3)', tipo: 'outras', valor: 1000, quantidade: 3 },
      { nome: 'Sem categoria', tipo: 'sem', valor: 400, quantidade: 1 },
      { nome: 'A conferir', tipo: 'conferir', valor: 100, quantidade: 1 }
    ];
    const { tela } = await montar(dados);
    const itens = tela.querySelectorAll('.pv-categorias-painel li');
    expect(itens.length).toBe(4);
    const link = itens[0].querySelector('a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toContain('/pages/contas-pagar?categoria=Compras&de=2026-11-10&ate=2026-12-09');
    expect((itens[0].querySelector('.pv-categorias-painel__barra span') as HTMLElement).style.width).toBe('100%');
    expect((itens[1].querySelector('.pv-categorias-painel__barra span') as HTMLElement).style.width).toBe('50%');
    expect(itens[1].querySelector('a')).toBeNull();
    expect(itens[2].querySelector('a')!.getAttribute('href')).toContain('categoria=Sem%20categoria');
    expect(itens[3].querySelector('a')).toBeNull();
    expect(itens[0].textContent).toContain('57%');
    expect(tela.textContent).toContain('ainda vai olhar no Bling');
  });

  it('sem categorias no resumo o card não aparece', async () => {
    const { tela } = await montar(resumo([1000, 2500, 0], 0));
    expect(tela.querySelector('.pv-categorias-painel')).toBeNull();
  });
});
