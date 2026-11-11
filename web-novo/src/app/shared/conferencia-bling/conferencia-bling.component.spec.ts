import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { PrevisioService } from '../../core/previsio.service';
import { ConferenciaBlingComponent } from './conferencia-bling.component';

describe('ConferenciaBlingComponent', () => {
  async function montar() {
    const api = {
      obterConferenciaBling: vi.fn(() => of({ conferirAntesDeLancar: true, exigeNumeroNota: false, toleranciaValor: 0.1, toleranciaDias: 0, fecharPendentes: true })),
      alterarConferenciaBling: vi.fn((_: number, dados: unknown) => of(dados))
    };
    TestBed.configureTestingModule({ imports: [ConferenciaBlingComponent], providers: [{ provide: PrevisioService, useValue: api }] });
    const fixture = TestBed.createComponent(ConferenciaBlingComponent);
    fixture.componentRef.setInput('clienteId', 7);
    fixture.autoDetectChanges();
    await fixture.whenStable();
    return { fixture, api, tela: fixture.nativeElement as HTMLElement };
  }

  it('carrega os padrões e salva o que foi mudado', async () => {
    const { fixture, api, tela } = await montar();
    expect((tela.querySelector('#conferencia-valor') as HTMLInputElement).value).toBe('0.10');
    expect((tela.querySelector('#conferencia-criterio') as HTMLSelectElement).value).toBe('simples');

    const criterio = tela.querySelector('#conferencia-criterio') as HTMLSelectElement;
    criterio.value = 'numero';
    criterio.dispatchEvent(new Event('change'));
    const dias = tela.querySelector('#conferencia-dias') as HTMLInputElement;
    dias.value = '2';
    dias.dispatchEvent(new Event('input'));
    const fechar = tela.querySelectorAll('input[type=checkbox]')[1] as HTMLInputElement;
    fechar.click();
    await fixture.whenStable();
    (tela.querySelector('.pv-formulario__acoes .btn-info') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(api.alterarConferenciaBling).toHaveBeenCalledWith(7,
      { conferirAntesDeLancar: true, exigeNumeroNota: true, toleranciaValor: 0.1, toleranciaDias: 2, fecharPendentes: false });
    expect(tela.querySelector('.alert-success')?.textContent).toContain('salva');
  });

  it('não salva fora dos limites', async () => {
    const { fixture, api, tela } = await montar();
    const valor = tela.querySelector('#conferencia-valor') as HTMLInputElement;
    valor.value = '7';
    valor.dispatchEvent(new Event('input'));
    fixture.componentInstance.salvar();
    await fixture.whenStable();
    expect(api.alterarConferenciaBling).not.toHaveBeenCalled();
    expect(tela.querySelector('.alert-danger')?.textContent).toContain('R$ 5,00');
  });
});
