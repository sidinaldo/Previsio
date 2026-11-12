import { HttpErrorResponse, HttpHeaders, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { nomeDoArquivo } from '../../core/download';
import { ExportarComponent } from './exportar.component';

function resposta(disposicao: string | null): HttpResponse<Blob> {
  let headers = new HttpHeaders();
  if (disposicao) {
    headers = headers.set('Content-Disposition', disposicao);
  }
  return new HttpResponse<Blob>({ body: new Blob(['x']), headers: headers, status: 200 });
}

describe('nomeDoArquivo', () => {
  it('lê o nome que a API mandou, com acento ou sem', () => {
    expect(nomeDoArquivo(resposta('attachment; filename=previsio-notas-2026-11-11-0900.xlsx; filename*=UTF-8\'\'previsio-notas-2026-11-11-0900.xlsx'), 'p.xlsx'))
      .toBe('previsio-notas-2026-11-11-0900.xlsx');
    expect(nomeDoArquivo(resposta('attachment; filename="contas.xlsx"'), 'p.xlsx')).toBe('contas.xlsx');
    expect(nomeDoArquivo(resposta('attachment; filename*=UTF-8\'\'situa%C3%A7%C3%A3o.xlsx'), 'p.xlsx')).toBe('situação.xlsx');
    expect(nomeDoArquivo(resposta(null), 'padrao.xlsx')).toBe('padrao.xlsx');
  });
});

describe('ExportarComponent', () => {
  async function montar(gerar: () => unknown) {
    TestBed.configureTestingModule({ imports: [ExportarComponent] });
    const fixture = TestBed.createComponent(ExportarComponent);
    fixture.componentRef.setInput('gerar', gerar);
    fixture.componentRef.setInput('nomePadrao', 'previsio-notas.xlsx');
    fixture.autoDetectChanges();
    await fixture.whenStable();
    return { fixture, tela: fixture.nativeElement as HTMLElement };
  }

  it('baixa a planilha com o nome da API', async () => {
    const criarUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:planilha');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const clique = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    const gerar = vi.fn(() => of(resposta('attachment; filename=previsio-notas-2026-11-11-0900.xlsx')));
    const { fixture, tela } = await montar(gerar);

    (tela.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(gerar).toHaveBeenCalledTimes(1);
    expect(criarUrl).toHaveBeenCalledTimes(1);
    expect(clique).toHaveBeenCalledTimes(1);
    const link = clique.mock.contexts[0] as HTMLAnchorElement;
    expect(link.download).toBe('previsio-notas-2026-11-11-0900.xlsx');
    expect(tela.querySelector('[role=alert]')).toBeNull();
    vi.restoreAllMocks();
  });

  it('mostra o motivo que a API mandou no erro (que chega como Blob)', async () => {
    const erro = new HttpErrorResponse({
      status: 400,
      error: new Blob(['São mais de 20.000 notas. Escolha um período menor ou uma empresa para exportar.'])
    });
    const { fixture, tela } = await montar(() => throwError(() => erro));

    (tela.querySelector('button') as HTMLButtonElement).click();
    await new Promise(resolve => setTimeout(resolve, 10));
    await fixture.whenStable();

    expect(tela.querySelector('[role=alert]')?.textContent).toContain('São mais de 20.000 notas');
    expect((tela.querySelector('button') as HTMLButtonElement).disabled).toBe(false);
  });
});
