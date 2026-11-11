import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { MenuAcoesComponent } from './menu-acoes.component';

@Component({
  imports: [MenuAcoesComponent],
  template: `
    <p id="fora">fora</p>
    <app-menu-acoes rotulo="Mais ações para Loja Centro">
      <button type="button" class="pv-acoes__item" (click)="escolhido.set('editar')">Editar</button>
      <button type="button" class="pv-acoes__item" (click)="escolhido.set('desativar')">Desativar</button>
    </app-menu-acoes>
  `
})
class TelaDeTeste {
  readonly escolhido = signal('');
}

async function montar() {
  TestBed.configureTestingModule({ imports: [TelaDeTeste] });
  const fixture = TestBed.createComponent(TelaDeTeste);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  const tela = fixture.nativeElement as HTMLElement;
  document.body.appendChild(tela);
  const gatilho = tela.querySelector('.pv-acoes__gatilho') as HTMLButtonElement;
  return { fixture, tela, gatilho };
}

describe('MenuAcoesComponent', () => {
  it('o botão "⋯" tem rótulo para leitor de tela e começa fechado', async () => {
    const { tela, gatilho } = await montar();
    expect(gatilho.getAttribute('aria-label')).toBe('Mais ações para Loja Centro');
    expect(gatilho.getAttribute('title')).toBe('Mais ações para Loja Centro');
    expect(gatilho.getAttribute('aria-haspopup')).toBe('menu');
    expect(gatilho.getAttribute('aria-expanded')).toBe('false');
    expect(tela.querySelector('[role=menu]')).toBeNull();
  });

  it('abre o menu, executa a ação escolhida e fecha', async () => {
    const { fixture, tela, gatilho } = await montar();
    gatilho.click();
    await fixture.whenStable();
    const menu = tela.querySelector('[role=menu]') as HTMLElement;
    expect(menu).not.toBeNull();
    expect(gatilho.getAttribute('aria-expanded')).toBe('true');
    const itens = Array.from(menu.querySelectorAll('.pv-acoes__item'));
    expect(itens.map(i => i.getAttribute('role'))).toEqual(['menuitem', 'menuitem']);

    (itens[1] as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(fixture.componentInstance.escolhido()).toBe('desativar');
    expect(tela.querySelector('[role=menu]')).toBeNull();
  });

  it('Esc fecha e devolve o foco ao botão; clicar fora também fecha', async () => {
    const { fixture, tela, gatilho } = await montar();
    gatilho.click();
    await fixture.whenStable();
    const menu = tela.querySelector('[role=menu]') as HTMLElement;
    menu.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await fixture.whenStable();
    expect(tela.querySelector('[role=menu]')).toBeNull();
    expect(document.activeElement).toBe(gatilho);

    gatilho.click();
    await fixture.whenStable();
    (tela.querySelector('#fora') as HTMLElement).click();
    await fixture.whenStable();
    expect(tela.querySelector('[role=menu]')).toBeNull();
  });
});
