import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { ResumoSemanal } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { ResumoSemanalComponent } from './resumo-semanal.component';

const SEGUNDA_7H: ResumoSemanal = { ativo: true, dia: 1, hora: 7, ultimoEnvioEm: null, destinatarios: 2 };

async function montar(api: Partial<Record<keyof PrevisioService, unknown>>) {
  TestBed.configureTestingModule({
    imports: [ResumoSemanalComponent],
    providers: [{ provide: PrevisioService, useValue: api }]
  });
  const fixture = TestBed.createComponent(ResumoSemanalComponent);
  fixture.componentRef.setInput('clienteId', 7);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { fixture, tela: fixture.nativeElement as HTMLElement };
}

function apiPadrao() {
  return {
    obterResumoSemanal: vi.fn(() => of(SEGUNDA_7H)),
    alterarResumoSemanal: vi.fn((_: number, dados: { ativo: boolean; dia: number; hora: number }) =>
      of({ ativo: dados.ativo, dia: dados.dia, hora: dados.hora, ultimoEnvioEm: null, destinatarios: 2 })),
    enviarResumoSemanalTeste: vi.fn(() => of(undefined))
  };
}

describe('ResumoSemanalComponent', () => {
  it('carrega a configuração da rede e diz para quantos usuários vai', async () => {
    const api = apiPadrao();
    const { tela } = await montar(api);
    expect(api.obterResumoSemanal).toHaveBeenCalledWith(7);
    const dia = tela.querySelector('#resumo-dia') as HTMLSelectElement;
    const hora = tela.querySelector('#resumo-hora') as HTMLSelectElement;
    expect(dia.options[dia.selectedIndex].text).toBe('Segunda-feira');
    expect(hora.options[hora.selectedIndex].text).toBe('07h');
    expect(tela.textContent).toContain('Vai para os 2 usuários da rede');
  });

  it('salva o dia e a hora escolhidos', async () => {
    const api = apiPadrao();
    const { fixture, tela } = await montar(api);
    fixture.componentInstance.dia.set(5);
    fixture.componentInstance.hora.set(16);
    (tela.querySelector('button[type=submit]') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.alterarResumoSemanal).toHaveBeenCalledWith(7, { ativo: true, dia: 5, hora: 16 });
    expect(tela.querySelector('.alert-success')?.textContent).toContain('Resumo salvo: sexta-feira, às 16h');
  });

  it('desligado, avisa que não vai mais e bloqueia dia e hora', async () => {
    const api = apiPadrao();
    const { fixture, tela } = await montar(api);
    fixture.componentInstance.ativo.set(false);
    await fixture.whenStable();
    expect((tela.querySelector('#resumo-dia') as HTMLSelectElement).disabled).toBe(true);
    (tela.querySelector('button[type=submit]') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(tela.querySelector('.alert-success')?.textContent).toContain('Resumo por e-mail desligado.');
  });

  it('"Enviar para mim agora" mostra o motivo quando a API recusa', async () => {
    const api = apiPadrao();
    api.enviarResumoSemanalTeste = vi.fn(() => throwError(() =>
      new HttpErrorResponse({ status: 400, error: 'O envio de e-mail não está configurado no servidor (seção Smtp).' })));
    const { fixture, tela } = await montar(api);
    // "Enviar para mim agora" fica à esquerda do Salvar
    const enviar = Array.from(tela.querySelectorAll('.pv-formulario__acoes button'))
      .find(b => b.textContent?.includes('Enviar para mim agora')) as HTMLButtonElement;
    enviar.click();
    await fixture.whenStable();
    expect(api.enviarResumoSemanalTeste).toHaveBeenCalledWith(7);
    expect(tela.querySelector('.alert-danger')?.textContent).toContain('seção Smtp');
  });

  it('"Todo dia útil" vem primeiro na lista e salva como dia 7', async () => {
    const api = apiPadrao();
    const { fixture, tela } = await montar(api);
    const dia = tela.querySelector('#resumo-dia') as HTMLSelectElement;
    expect(dia.options[0].text).toBe('Todo dia útil (seg a sex)');
    fixture.componentInstance.dia.set(7);
    (tela.querySelector('button[type=submit]') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.alterarResumoSemanal).toHaveBeenCalledWith(7, { ativo: true, dia: 7, hora: 7 });
    expect(tela.querySelector('.alert-success')?.textContent).toContain('Resumo salvo: todo dia útil, às 07h.');
  });

  it('recarrega quando o cliente muda', async () => {
    const api = apiPadrao();
    const { fixture } = await montar(api);
    fixture.componentRef.setInput('clienteId', 9);
    await fixture.whenStable();
    expect(api.obterResumoSemanal).toHaveBeenLastCalledWith(9);
  });
});
