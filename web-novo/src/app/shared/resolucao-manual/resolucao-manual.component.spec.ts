import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { ResolucaoManualComponent } from './resolucao-manual.component';
import { PREVISIO_CONFIG } from '../../core/previsio.config';

const API = PREVISIO_CONFIG.apiUrl;

async function montar() {
  TestBed.configureTestingModule({
    imports: [ResolucaoManualComponent],
    providers: [provideHttpClient(), provideHttpClientTesting()]
  });
  const fixture = TestBed.createComponent(ResolucaoManualComponent);
  fixture.componentRef.setInput('notaId', 15);
  fixture.componentRef.setInput('numero', '48213');
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { fixture, api: TestBed.inject(HttpTestingController), tela: fixture.nativeElement as HTMLElement };
}

describe('ResolucaoManualComponent', () => {
  it('confirma com a observação e avisa que concluiu', async () => {
    const { fixture, api, tela } = await montar();
    let concluiu = false;
    fixture.componentInstance.concluido.subscribe(() => (concluiu = true));
    expect(tela.textContent).toContain('Marcar a NF 48213 como lançada manualmente no Bling?');

    fixture.componentInstance.observacao.set('  lançada como conta 1234 ');
    fixture.componentInstance.confirmar();

    const chamada = api.expectOne(`${API}/notas/15/lancada-manualmente`);
    expect(chamada.request.method).toBe('POST');
    expect(chamada.request.body).toEqual({ observacao: 'lançada como conta 1234' });
    chamada.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(concluiu).toBe(true);
  });

  it('mostra o motivo quando a API recusa e não conclui', async () => {
    const { fixture, api, tela } = await montar();
    let concluiu = false;
    fixture.componentInstance.concluido.subscribe(() => (concluiu = true));

    fixture.componentInstance.confirmar();
    api.expectOne(`${API}/notas/15/lancada-manualmente`).flush(
      'Só notas com problema podem ser marcadas como lançadas manualmente no Bling.',
      { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    expect(concluiu).toBe(false);
    expect(tela.textContent).toContain('Só notas com problema');
  });

  it('Cancelar não chama a API', async () => {
    const { fixture, api, tela } = await montar();
    let cancelou = false;
    fixture.componentInstance.cancelado.subscribe(() => (cancelou = true));

    (Array.from(tela.querySelectorAll('button')).find(b => b.textContent!.includes('Cancelar')) as HTMLButtonElement).click();

    expect(cancelou).toBe(true);
    api.verify();
  });
});
