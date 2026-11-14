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

  it('indicadores num grid só: 4 sem fluxo de caixa e 7 com fluxo', async () => {
    const semFluxo = await montar(resumo([1000, 2500, 0], 0));
    expect(semFluxo.tela.querySelectorAll('.pv-kpis > .pv-kpi').length).toBe(4);
    expect(semFluxo.tela.querySelector('.pv-kpis .row')).toBeNull();
    TestBed.resetTestingModule();

    const comFluxo = resumo([1000, 2500, 0], 0);
    comFluxo.fluxoCaixaAtivo = true;
    comFluxo.saldoPeriodo = -500;
    const { tela } = await montar(comFluxo);
    expect(tela.querySelectorAll('.pv-kpis > .pv-kpi').length).toBe(7);
    // "?" do saldo descreve o cálculo para leitor de tela
    const ajuda = tela.querySelector('.pv-ajuda__botao') as HTMLButtonElement;
    expect(ajuda.getAttribute('aria-describedby')).toBe('pv-ajuda-saldo');
    expect(tela.querySelector('#pv-ajuda-saldo')!.textContent).toContain('Não considera o saldo atual das contas bancárias');
  });

  it('tooltip do gráfico mostra a semana da barra com centavos e some ao sair', async () => {
    const dados = resumo([1000.5, 2500, 0], 0);
    dados.fluxoCaixaAtivo = true;
    dados.semanas[1].entradas = 3200.25;
    dados.semanas[1].saldo = 700.25;
    const { tela } = await montar(dados);
    const area = tela.querySelector('.pv-grafico-area') as HTMLElement;
    // O Chartist é simulado: põe uma "barra" como o draw faria
    const barra = document.createElement('span');
    barra.setAttribute('data-semana', '1');
    area.appendChild(barra);

    barra.dispatchEvent(new MouseEvent('mouseover', { bubbles: true }));
    await new Promise(r => setTimeout(r));
    const dica = tela.querySelector('#pv-grafico-dica') as HTMLElement;
    expect(dica.getAttribute('role')).toBe('tooltip');
    expect(dica.textContent).toContain('17/11');
    // Com centavos (o separador depende da localidade do teste)
    expect(dica.textContent).toMatch(/Entradas: R\$\s?3[.,]200[.,]25/);
    expect(dica.textContent).toMatch(/Saídas: R\$\s?2[.,]500[.,]00/);
    expect(dica.textContent).toMatch(/Saldo: R\$\s?700[.,]25/);

    barra.dispatchEvent(new MouseEvent('mouseout', { bubbles: true }));
    await new Promise(r => setTimeout(r));
    expect(tela.querySelector('#pv-grafico-dica')).toBeNull();

    barra.dispatchEvent(new FocusEvent('focusin', { bubbles: true }));
    await new Promise(r => setTimeout(r));
    expect(tela.querySelector('#pv-grafico-dica')).not.toBeNull();
  });

  it('resumo semanal usa as mesmas colunas do gráfico', async () => {
    const { tela } = await montar(resumo([1000, 2500, 0], 0));
    const lista = tela.querySelector('.pv-semanas--alinhado') as HTMLElement;
    expect(lista.style.getPropertyValue('--pv-semanas-colunas')).toBe('3');
  });

  it('com mais de 20% sem categoria mostra o atalho para Configurar categorias', async () => {
    const dados = resumo([1000, 2500, 0], 0);
    dados.categorias = [
      { nome: 'Compras', tipo: 'categoria', valor: 700, quantidade: 2 },
      { nome: 'Sem categoria', tipo: 'sem', valor: 300, quantidade: 1 }
    ];
    const { tela } = await montar(dados);
    const acao = tela.querySelector('.pv-categorias-painel__acao') as HTMLElement;
    expect(acao.textContent).toContain('30% das saídas estão sem categoria');
    expect(acao.querySelector('a')!.getAttribute('href')).toContain('/pages/configuracoes#categorias');
  });

  it('até 20% sem categoria não mostra o atalho', async () => {
    const dados = resumo([1000, 2500, 0], 0);
    dados.categorias = [
      { nome: 'Compras', tipo: 'categoria', valor: 800, quantidade: 2 },
      { nome: 'Sem categoria', tipo: 'sem', valor: 200, quantidade: 1 }
    ];
    const { tela } = await montar(dados);
    expect(tela.querySelector('.pv-categorias-painel')).not.toBeNull();
    expect(tela.querySelector('.pv-categorias-painel__acao')).toBeNull();
  });

  it('Por empresa: rótulos revisados e Total com traço sem notas fora do Bling', async () => {
    const dados = resumo([1000, 2500, 0], 0);
    dados.empresas = [{
      empresaId: 1, nome: 'Matriz', cnpj: '11222333000181', quantidadeContas: 3, valor: 3500,
      notasPendentes: 0, valorNotasPendentes: 0, proximoVencimento: null, valorProximoVencimento: 0, ultimaSincronizacaoEm: null
    }];
    const { tela } = await montar(dados);
    const cabecalho = Array.from(tela.querySelectorAll('th')).map(th => th.textContent!.trim());
    expect(cabecalho).toContain('Contas a pagar');
    expect(cabecalho).toContain('Notas fora do Bling');
    expect(cabecalho).toContain('Próximo a pagar');
    const total = tela.querySelector('.pv-tabela__total') as HTMLElement;
    expect(total.textContent).toContain('—');
    expect(total.textContent).not.toContain('0 ·');
  });

  it('concordância: 1 nota, nenhuma nota e "da empresa" no gráfico', async () => {
    const umaNota = resumo([1000, 2500, 0], 1);
    umaNota.empresas = [{
      empresaId: 1, nome: 'Matriz', cnpj: '11222333000181', quantidadeContas: 3, valor: 3500,
      notasPendentes: 1, valorNotasPendentes: 100, proximoVencimento: null, valorProximoVencimento: 0, ultimaSincronizacaoEm: null
    }];
    const primeira = await montar(umaNota);
    expect(primeira.tela.textContent).toContain('1 nota para resolver');
    expect(primeira.tela.textContent).not.toContain('1 notas');
    expect(primeira.tela.textContent).toContain('no Bling da empresa, semana a semana');
    TestBed.resetTestingModule();

    const { tela } = await montar(resumo([1000, 2500, 0], 0));
    expect(tela.textContent).toContain('Nenhuma nota para resolver');
  });

  it('categoria não identificada tem link para o filtro dela e não conta para o atalho', async () => {
    const dados = resumo([1000, 2500, 0], 0);
    dados.categorias = [
      { nome: 'Compras', tipo: 'categoria', valor: 700, quantidade: 2 },
      { nome: 'Categoria não identificada', tipo: 'naoidentificada', valor: 300, quantidade: 1 }
    ];
    const { tela } = await montar(dados);
    const itens = tela.querySelectorAll('.pv-categorias-painel li');
    expect(itens[1].querySelector('a')!.getAttribute('href')).toContain('categoria=Categoria%20n%C3%A3o%20identificada');
    expect(tela.querySelector('.pv-categorias-painel__acao')).toBeNull();
  });
});
