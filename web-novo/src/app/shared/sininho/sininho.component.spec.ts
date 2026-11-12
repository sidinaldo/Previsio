import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { PainelAvisos } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';
import { SininhoComponent } from './sininho.component';

function painelCom(novos: number): PainelAvisos {
  return {
    novos: novos,
    total: 2,
    itens: [
      {
        id: 7, tipo: 'NotaComProblema', titulo: 'Nota 48213 sem vencimentos',
        mensagem: 'A nota 48213 da Distribuidora Alfa chegou sem vencimentos no XML.',
        link: '/pages/notas-pendentes', clienteId: 2, clienteNome: 'Rede Auto Center',
        criadoEm: '2026-11-11T13:20:00Z', novo: true
      },
      {
        id: 8, tipo: 'BlingDesconectado', titulo: 'Bling desconectado: Loja Centro',
        mensagem: 'Reconecte a empresa para voltar a lançar as notas.',
        link: '/pages/empresas', clienteId: 1, clienteNome: 'Autopeças Piloto',
        criadoEm: '2026-11-10T09:00:00Z', novo: false
      }
    ]
  };
}

async function montar(painel: PainelAvisos, admin = true) {
  const api = {
    obterAvisos: vi.fn(() => of(painel)),
    marcarAvisosVistos: vi.fn(() => of(undefined))
  };
  const sessao = {
    ehAdmin: signal(admin),
    clienteId: signal<number | null>(1),
    escolherCliente: vi.fn()
  };
  TestBed.configureTestingModule({
    imports: [SininhoComponent],
    providers: [
      provideRouter([]),
      { provide: PrevisioService, useValue: api },
      { provide: SessaoService, useValue: sessao }
    ]
  });
  const fixture = TestBed.createComponent(SininhoComponent);
  fixture.autoDetectChanges();
  // O primeiro ciclo do timer roda logo depois de montar
  await new Promise(resolve => setTimeout(resolve, 5));
  await fixture.whenStable();
  const tela = fixture.nativeElement as HTMLElement;
  document.body.appendChild(tela);
  const gatilho = tela.querySelector('.pv-sininho__gatilho') as HTMLButtonElement;
  return { fixture, tela, gatilho, api, sessao };
}

describe('SininhoComponent', () => {
  it('busca os avisos ao montar e mostra quantos são novos', async () => {
    const { tela, gatilho, api } = await montar(painelCom(1));
    expect(api.obterAvisos).toHaveBeenCalledTimes(1);
    expect(tela.querySelector('.pv-sininho__contador')?.textContent?.trim()).toBe('1');
    expect(gatilho.getAttribute('aria-label')).toBe('Avisos: 1 novo');
    expect(tela.querySelector('[role=dialog]')).toBeNull();
  });

  it('sem avisos novos não mostra o número', async () => {
    const { tela, gatilho } = await montar(painelCom(0));
    expect(tela.querySelector('.pv-sininho__contador')).toBeNull();
    expect(gatilho.getAttribute('aria-label')).toBe('Avisos');
  });

  it('ao abrir lista os avisos, zera o número e marca como vistos', async () => {
    const { fixture, tela, gatilho, api } = await montar(painelCom(1));
    gatilho.click();
    await fixture.whenStable();

    const itens = tela.querySelectorAll('.pv-sininho__item');
    expect(itens.length).toBe(2);
    expect(itens[0].textContent).toContain('Nota 48213 sem vencimentos');
    expect(itens[0].textContent).toContain('Rede Auto Center');
    expect(itens[0].classList.contains('pv-sininho__item--novo')).toBe(true);
    expect(tela.querySelector('.pv-sininho__total')?.textContent?.trim()).toBe('2 em aberto');
    expect(tela.querySelector('.pv-sininho__contador')).toBeNull();
    expect(api.marcarAvisosVistos).toHaveBeenCalledTimes(1);
  });

  it('sem nada novo, abrir não chama a API de vistos', async () => {
    const { fixture, gatilho, api } = await montar(painelCom(0));
    gatilho.click();
    await fixture.whenStable();
    expect(api.marcarAvisosVistos).not.toHaveBeenCalled();
  });

  it('sem avisos mostra "Tudo em dia"', async () => {
    const { fixture, tela, gatilho } = await montar({ novos: 0, total: 0, itens: [] });
    gatilho.click();
    await fixture.whenStable();
    expect(tela.querySelector('.pv-sininho__vazio')?.textContent).toContain('Tudo em dia');
  });

  it('clicar no aviso de outro cliente troca o cliente e abre a tela do problema', async () => {
    const { fixture, tela, gatilho, sessao } = await montar(painelCom(1));
    const router = TestBed.inject(Router);
    const navegar = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    gatilho.click();
    await fixture.whenStable();

    (tela.querySelectorAll('.pv-sininho__item')[0] as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(sessao.escolherCliente).toHaveBeenCalledWith(2);
    expect(navegar).toHaveBeenCalledWith('/pages/notas-pendentes');
    expect(tela.querySelector('[role=dialog]')).toBeNull();
  });

  it('aviso do cliente já escolhido não troca o cliente', async () => {
    const { fixture, tela, gatilho, sessao } = await montar(painelCom(0));
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    gatilho.click();
    await fixture.whenStable();
    (tela.querySelectorAll('.pv-sininho__item')[1] as HTMLButtonElement).click();
    expect(sessao.escolherCliente).not.toHaveBeenCalled();
  });

  it('usuário do cliente não vê o nome do cliente nem troca de cliente', async () => {
    const { fixture, tela, gatilho, sessao } = await montar(painelCom(0), false);
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    gatilho.click();
    await fixture.whenStable();
    const primeiro = tela.querySelectorAll('.pv-sininho__item')[0] as HTMLButtonElement;
    expect(primeiro.textContent).not.toContain('Rede Auto Center');
    primeiro.click();
    expect(sessao.escolherCliente).not.toHaveBeenCalled();
  });

  it('fecha com Esc e ao clicar fora', async () => {
    const { fixture, tela, gatilho } = await montar(painelCom(0));
    gatilho.click();
    await fixture.whenStable();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await fixture.whenStable();
    expect(tela.querySelector('[role=dialog]')).toBeNull();

    gatilho.click();
    await fixture.whenStable();
    document.body.click();
    await fixture.whenStable();
    expect(tela.querySelector('[role=dialog]')).toBeNull();
  });
});
