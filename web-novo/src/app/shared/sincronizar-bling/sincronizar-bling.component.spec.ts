import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { PrevisioService } from '../../core/previsio.service';
import { SincronizarBlingComponent } from './sincronizar-bling.component';

describe('SincronizarBlingComponent', () => {
  function montar(api: Partial<Record<'sincronizarRede' | 'situacaoSincronizacao', unknown>>) {
    TestBed.configureTestingModule({ imports: [SincronizarBlingComponent], providers: [{ provide: PrevisioService, useValue: api }] });
    const fixture = TestBed.createComponent(SincronizarBlingComponent);
    const avisos: string[] = [];
    let sincronizado = 0;
    fixture.componentInstance.aviso.subscribe(aviso => avisos.push(aviso.tipo + ':' + aviso.texto));
    fixture.componentInstance.sincronizado.subscribe(() => sincronizado++);
    fixture.detectChanges();
    return { fixture, avisos, sincronizado: () => sincronizado };
  }

  it('pede a sincronização, espera terminar e avisa a tela', async () => {
    vi.useFakeTimers();
    const api = {
      sincronizarRede: vi.fn(() => of({ emExecucao: true, ultimaSincronizacaoEm: null, podeSincronizarEm: null })),
      situacaoSincronizacao: vi.fn(() => of({ emExecucao: false, ultimaSincronizacaoEm: null, podeSincronizarEm: null }))
    };
    const { fixture, avisos, sincronizado } = montar(api);

    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    expect(fixture.componentInstance.sincronizando()).toBe(true);
    await vi.advanceTimersByTimeAsync(5000);

    expect(api.sincronizarRede).toHaveBeenCalled();
    expect(sincronizado()).toBe(1);
    expect(fixture.componentInstance.sincronizando()).toBe(false);
    expect(avisos[avisos.length - 1]).toContain('sucesso:');
    vi.useRealTimers();
  });

  it('mostra o motivo quando a API recusa (sincronizou há pouco)', () => {
    const recusa = new HttpErrorResponse({ status: 409, error: 'As contas foram sincronizadas há pouco. Tente de novo em 90 segundos.' });
    const api = { sincronizarRede: vi.fn(() => throwError(() => recusa)), situacaoSincronizacao: vi.fn() };
    const { fixture, avisos, sincronizado } = montar(api);

    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();

    expect(avisos[avisos.length - 1]).toBe('info:As contas foram sincronizadas há pouco. Tente de novo em 90 segundos.');
    expect(sincronizado()).toBe(0);
    expect(api.situacaoSincronizacao).not.toHaveBeenCalled();
    expect(fixture.componentInstance.sincronizando()).toBe(false);
  });
});
