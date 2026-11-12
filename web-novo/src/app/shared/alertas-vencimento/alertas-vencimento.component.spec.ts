import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { PrevisioService } from '../../core/previsio.service';
import { AlertasVencimentoComponent } from './alertas-vencimento.component';

describe('AlertasVencimentoComponent', () => {
  async function montar() {
    const api = {
      obterAlertasVencimento: vi.fn(() => of({ avisoVencidasDias: 1, alertaDiarioAtivo: true, alertaDiarioHora: 7 })),
      alterarAlertasVencimento: vi.fn((_: number, dados: unknown) => of(dados))
    };
    TestBed.configureTestingModule({ imports: [AlertasVencimentoComponent], providers: [{ provide: PrevisioService, useValue: api }] });
    const fixture = TestBed.createComponent(AlertasVencimentoComponent);
    fixture.componentRef.setInput('clienteId', 7);
    fixture.autoDetectChanges();
    await fixture.whenStable();
    return { fixture, api, tela: fixture.nativeElement as HTMLElement };
  }

  it('carrega a configuração da rede', async () => {
    const { tela, api } = await montar();
    expect(api.obterAlertasVencimento).toHaveBeenCalledWith(7);
    expect((tela.querySelector('#alerta-dias') as HTMLInputElement).value).toBe('1');
    expect(tela.textContent).toContain('dia de atraso');
    expect((tela.querySelector('#alerta-hora') as HTMLSelectElement).value).toBe('7');
  });

  it('salva os dias de atraso, o e-mail diário e a hora', async () => {
    const { fixture, api, tela } = await montar();
    const dias = tela.querySelector('#alerta-dias') as HTMLInputElement;
    dias.value = '3';
    dias.dispatchEvent(new Event('input'));
    const hora = tela.querySelector('#alerta-hora') as HTMLSelectElement;
    hora.value = '6';
    hora.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(tela.textContent).toContain('dias de atraso');
    (tela.querySelector('.pv-formulario__acoes .btn-info') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.alterarAlertasVencimento).toHaveBeenCalledWith(7, { avisoVencidasDias: 3, alertaDiarioAtivo: true, alertaDiarioHora: 6 });
    expect(tela.querySelector('.alert-success')?.textContent).toContain('salvos');
  });

  it('dias fora de 1 a 60 não salvam', async () => {
    const { fixture, api, tela } = await montar();
    fixture.componentInstance.dias.set(0);
    fixture.componentInstance.salvar();
    await fixture.whenStable();
    expect(api.alterarAlertasVencimento).not.toHaveBeenCalled();
    expect(tela.querySelector('.alert-danger')?.textContent).toContain('1 a 60');
  });
});
