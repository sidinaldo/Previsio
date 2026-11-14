import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { PaginacaoComponent } from './paginacao.component';
import { paginaValida, paginar } from '../../core/paginacao';

@Component({
  imports: [PaginacaoComponent],
  template: `<app-paginacao [total]="total()" [(pagina)]="pagina" [(tamanho)]="tamanho" />`
})
class TelaDeTeste {
  readonly total = signal(37);
  readonly pagina = signal(1);
  readonly tamanho = signal(10);
}

@Component({
  imports: [PaginacaoComponent],
  template: `<div class="card"><app-paginacao [total]="total()" [(pagina)]="pagina" [(tamanho)]="tamanho" /></div>`
})
class TelaComCard {
  readonly total = signal(250);
  readonly pagina = signal(1);
  readonly tamanho = signal(100);
}

async function montarComCard(topoDoCard: number) {
  TestBed.configureTestingModule({ imports: [TelaComCard] });
  const fixture = TestBed.createComponent(TelaComCard);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  const tela = fixture.nativeElement as HTMLElement;
  const card = tela.querySelector('.card') as HTMLElement;
  card.getBoundingClientRect = () => ({ top: topoDoCard } as DOMRect);
  const rolar = vi.fn();
  card.scrollIntoView = rolar;
  return { fixture, tela, rolar };
}

async function montar() {
  TestBed.configureTestingModule({ imports: [TelaDeTeste] });
  const fixture = TestBed.createComponent(TelaDeTeste);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { fixture, tela: fixture.nativeElement as HTMLElement };
}

function botao(tela: HTMLElement, rotulo: string): HTMLButtonElement {
  return tela.querySelector(`button[aria-label="${rotulo}"]`) as HTMLButtonElement;
}

describe('paginação', () => {
  it('paginar devolve só os itens da página e corrige página que não existe mais', () => {
    const lista = Array.from({ length: 23 }, (_, i) => i + 1);
    expect(paginar(lista, 1, 10)).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
    expect(paginar(lista, 3, 10)).toEqual([21, 22, 23]);
    expect(paginar(lista, 9, 10)).toEqual([21, 22, 23]);
    expect(paginaValida(0, 23, 10)).toBe(1);
    expect(paginar([], 1, 10)).toEqual([]);
  });

  it('mostra a faixa, anda pelas páginas e trava nas pontas', async () => {
    const { fixture, tela } = await montar();
    expect(tela.textContent).toContain('Mostrando 1–10 de 37');
    expect(tela.textContent).toContain('Página 1 de 4');
    expect(botao(tela, 'Página anterior').disabled).toBe(true);

    botao(tela, 'Próxima página').click();
    await fixture.whenStable();
    expect(fixture.componentInstance.pagina()).toBe(2);
    expect(tela.textContent).toContain('Mostrando 11–20 de 37');

    botao(tela, 'Última página').click();
    await fixture.whenStable();
    expect(tela.textContent).toContain('Mostrando 31–37 de 37');
    expect(botao(tela, 'Próxima página').disabled).toBe(true);
  });

  it('trocar o "Por página" volta para a primeira página', async () => {
    const { fixture, tela } = await montar();
    fixture.componentInstance.pagina.set(3);
    await fixture.whenStable();

    const select = tela.querySelector('select') as HTMLSelectElement;
    select.value = '25';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(fixture.componentInstance.tamanho()).toBe(25);
    expect(fixture.componentInstance.pagina()).toBe(1);
    expect(tela.textContent).toContain('Página 1 de 2');
  });

  it('some quando tudo cabe numa página', async () => {
    const { fixture, tela } = await montar();
    fixture.componentInstance.total.set(8);
    await fixture.whenStable();
    expect(tela.querySelector('nav')).toBeNull();
    expect(tela.querySelector('app-paginacao')!.classList.contains('pv-paginacao')).toBe(false);
  });

  it('âncora: trocar de 100 para 10 com o topo da tabela fora da tela rola até o card', async () => {
    const { tela, rolar } = await montarComCard(-1200);
    const seletor = tela.querySelector('select') as HTMLSelectElement;
    seletor.value = '10';
    seletor.dispatchEvent(new Event('change'));
    await new Promise(r => setTimeout(r));
    expect(rolar).toHaveBeenCalledTimes(1);
    expect(rolar.mock.calls[0][0].block).toBe('start');

    botao(tela, 'Próxima página').click();
    await new Promise(r => setTimeout(r));
    expect(rolar).toHaveBeenCalledTimes(2);
  });

  it('âncora: com o topo da tabela visível não rola', async () => {
    const { tela, rolar } = await montarComCard(120);
    botao(tela, 'Próxima página').click();
    await new Promise(r => setTimeout(r));
    expect(rolar).not.toHaveBeenCalled();
  });
});
