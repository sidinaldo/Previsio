import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { PrevisioService } from '../../core/previsio.service';
import { FluxoCaixaComponent } from './fluxo-caixa.component';

describe('FluxoCaixaComponent', () => {
  async function montar(ativo: boolean) {
    const api = {
      obterFluxoCaixa: vi.fn(() => of({ ativo })),
      alterarFluxoCaixa: vi.fn((_: number, dados: { ativo: boolean }) => of(dados))
    };
    TestBed.configureTestingModule({ imports: [FluxoCaixaComponent], providers: [{ provide: PrevisioService, useValue: api }] });
    const fixture = TestBed.createComponent(FluxoCaixaComponent);
    fixture.componentRef.setInput('clienteId', 7);
    fixture.autoDetectChanges();
    await fixture.whenStable();
    return { fixture, api, tela: fixture.nativeElement as HTMLElement };
  }

  it('carrega a configuração da rede', async () => {
    const { tela, api } = await montar(true);
    expect(api.obterFluxoCaixa).toHaveBeenCalledWith(7);
    expect((tela.querySelector('#fluxo-caixa-ativo') as HTMLInputElement).checked).toBe(true);
  });

  it('desliga e salva', async () => {
    const { fixture, api, tela } = await montar(true);
    const caixa = tela.querySelector('#fluxo-caixa-ativo') as HTMLInputElement;
    caixa.checked = false;
    caixa.dispatchEvent(new Event('change'));
    (tela.querySelector('.pv-formulario__acoes .btn-info') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.alterarFluxoCaixa).toHaveBeenCalledWith(7, { ativo: false });
    expect(tela.querySelector('.alert-success')?.textContent).toContain('desligado');
  });
});
