import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { ClientesService } from '../../core/clientes.service';
import { Cliente, seloContrato } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';
import { FaixaTesteComponent } from './faixa-teste.component';

function rede(parcial: Partial<Cliente>): Cliente {
  return {
    id: 7, nome: 'Rede Auto Center', imapHost: '', imapPorta: 993, imapSsl: true, imapUsuario: '',
    blingClientId: null, blingClientSecretCadastrado: false, ativo: true, contrato: 'Contratado', ...parcial
  };
}

async function montar(admin: boolean, cliente: Cliente) {
  const api = { obterCliente: vi.fn(() => of(cliente)) };
  TestBed.configureTestingModule({
    imports: [FaixaTesteComponent],
    providers: [
      provideRouter([]),
      { provide: PrevisioService, useValue: api },
      { provide: SessaoService, useValue: { ehAdmin: signal(admin), clienteId: signal(7) } },
      { provide: ClientesService, useValue: { lista: signal([cliente]) } }
    ]
  });
  const fixture = TestBed.createComponent(FaixaTesteComponent);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { tela: fixture.nativeElement as HTMLElement, api };
}

describe('FaixaTesteComponent', () => {
  it('rede contratada não mostra faixa', async () => {
    const { tela } = await montar(false, rede({}));
    expect(tela.querySelector('.pv-faixa-teste')).toBeNull();
  });

  it('usuário do cliente vê os dias que faltam e o aviso de que vai parar', async () => {
    const { tela, api } = await montar(false, rede({ contrato: 'EmTeste', diasRestantesTeste: 5, testeUltimoDia: '2026-11-15T00:00:00' }));
    expect(api.obterCliente).toHaveBeenCalledWith(7);
    const faixa = tela.querySelector('.pv-faixa-teste')!;
    expect(faixa.textContent).toContain('Teste grátis: faltam 5 dias');
    expect(faixa.textContent).toContain('até 15/11/2026');
    expect(faixa.textContent).toContain('fale com a Soft.IO');
    expect(faixa.classList.contains('pv-faixa-teste--fim')).toBe(false);
  });

  it('admin vê a faixa do cliente escolhido com o link para contratar, sem buscar na API', async () => {
    const { tela, api } = await montar(true, rede({ contrato: 'TesteEncerrado', diasRestantesTeste: 0, testeUltimoDia: '2026-11-10T00:00:00' }));
    expect(api.obterCliente).not.toHaveBeenCalled();
    const faixa = tela.querySelector('.pv-faixa-teste--encerrado')!;
    expect(faixa.textContent).toContain('O teste grátis acabou em 10/11/2026');
    expect(faixa.querySelector('a')?.textContent).toContain('Contratar ou prorrogar');
  });

  it('selo do contrato', () => {
    expect(seloContrato('EmTeste', 1).texto).toBe('Teste · último dia');
    expect(seloContrato('EmTeste', 9)).toEqual({ texto: 'Teste · faltam 9 dias', classe: 'badge badge-info' });
    expect(seloContrato('TesteEncerrado', 0).classe).toBe('badge badge-danger');
    expect(seloContrato(undefined, null).texto).toBe('Contratado');
  });
});
