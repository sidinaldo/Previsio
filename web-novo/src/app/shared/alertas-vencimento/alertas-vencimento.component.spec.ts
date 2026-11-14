import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';

import { PrevisioService } from '../../core/previsio.service';
import { AlertasVencimentoComponent } from './alertas-vencimento.component';

describe('AlertasVencimentoComponent', () => {
  async function montar(falhaTeste?: string) {
    const api = {
      enviarAlertaVencimentosTeste: vi.fn(() => {
        if (falhaTeste) {
          return throwError(() => new HttpErrorResponse({ status: 400, error: falhaTeste }));
        }
        return of(undefined);
      }),
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
  it('"Enviar para mim agora" fica antes do Salvar, envia e mostra o motivo quando a API recusa', async () => {
    const { fixture, api, tela } = await montar();
    const botoes = Array.from(tela.querySelectorAll('.pv-formulario__acoes button'));
    expect(botoes[0].textContent).toContain('Enviar para mim agora');
    expect(botoes[1].textContent).toContain('Salvar');
    (botoes[0] as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.enviarAlertaVencimentosTeste).toHaveBeenCalledWith(7);
    expect(api.alterarAlertasVencimento).not.toHaveBeenCalled();
    expect(tela.querySelector('.alert-success')?.textContent).toContain('enviado para o seu e-mail');

    TestBed.resetTestingModule();
    const recusa = await montar('Hoje não há aviso para enviar.');
    (recusa.tela.querySelector('.pv-formulario__acoes .btn-secundario') as HTMLButtonElement).click();
    await recusa.fixture.whenStable();
    expect(recusa.tela.querySelector('.alert-danger')?.textContent).toContain('Hoje não há aviso para enviar');
  });
});
