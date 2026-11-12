import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { ProvedorEmailComponent } from './provedor-email.component';

@Component({
  imports: [ProvedorEmailComponent],
  template: `<app-provedor-email [(host)]="host" [(porta)]="porta" [(ssl)]="ssl" [destino]="destino()" />`
})
class TelaDeTeste {
  readonly host = signal('imap.gmail.com');
  readonly porta = signal<number | null>(993);
  readonly ssl = signal(true);
  readonly destino = signal<string | null>('notas@rede.com.br');
}

async function montar() {
  TestBed.configureTestingModule({ imports: [TelaDeTeste] });
  const fixture = TestBed.createComponent(TelaDeTeste);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { fixture, tela: fixture.nativeElement as HTMLElement, dados: fixture.componentInstance };
}

function escolher(tela: HTMLElement, valor: string): void {
  const select = tela.querySelector('select') as HTMLSelectElement;
  select.value = valor;
  select.dispatchEvent(new Event('change'));
}

describe('ProvedorEmailComponent', () => {
  it('mostra o provedor do servidor atual e a instrução da senha', async () => {
    const { tela } = await montar();
    expect((tela.querySelector('select') as HTMLSelectElement).value).toBe('gmail');
    expect(tela.textContent).toContain('senha de app');
    expect(tela.textContent).toContain('notas@rede.com.br');
  });

  it('escolher um provedor preenche servidor, porta e SSL', async () => {
    const { fixture, tela, dados } = await montar();
    dados.porta.set(143);
    dados.ssl.set(false);
    await fixture.whenStable();
    escolher(tela, 'locaweb');
    await fixture.whenStable();
    expect(dados.host()).toBe('email-ssl.com.br');
    expect(dados.porta()).toBe(993);
    expect(dados.ssl()).toBe(true);
  });

  it('Microsoft avisa e abre o passo a passo do encaminhamento', async () => {
    const { fixture, tela } = await montar();
    escolher(tela, 'microsoft');
    await fixture.whenStable();
    expect(tela.querySelector('.alert-warning')?.textContent).toContain('encaminhe as notas');
    expect((tela.querySelector('details') as HTMLDetailsElement).open).toBe(true);
  });

  it('"Outro" limpa o servidor da lista para digitar o da hospedagem', async () => {
    const { fixture, tela, dados } = await montar();
    escolher(tela, 'outro');
    await fixture.whenStable();
    expect(dados.host()).toBe('');
    expect(tela.textContent).toContain('mail.seudominio.com.br');
  });
});
