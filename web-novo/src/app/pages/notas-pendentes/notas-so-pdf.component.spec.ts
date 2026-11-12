import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { NotaSoPdf } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { NotasSoPdfComponent } from './notas-so-pdf.component';

function nota(id: number, situacao: NotaSoPdf['situacao'], parcial: Partial<NotaSoPdf> = {}): NotaSoPdf {
  return {
    id: id, recebidaEm: '2026-11-11T14:00:00Z', remetente: 'faturamento@alfa.com.br', nomeRemetente: 'Alfa Distribuidora',
    assunto: 'NF-e 92001', pdfNome: 'danfe.pdf', empresaCaixaNome: null, chaveAcesso: null, origemChave: null,
    numeroNota: null, cnpjFornecedor: null, situacao: situacao, motivo: null, tentativasSefaz: 0,
    proximaTentativaEm: null, cienciaEnviadaEm: null, xmlPedidoEm: null, ...parcial
  };
}

function apiCom(notas: NotaSoPdf[]) {
  return {
    obterNotasSoPdf: vi.fn(() => of(notas)),
    baixarPdfNotaSoPdf: vi.fn(),
    anexarXmlNotaSoPdf: vi.fn(() => of(undefined)),
    informarChaveNotaSoPdf: vi.fn(() => of(undefined)),
    pedirXmlNotaSoPdf: vi.fn(() => of(undefined)),
    marcarNotaSoPdfLancada: vi.fn(() => of(undefined))
  };
}

async function montar(api: ReturnType<typeof apiCom>, empresaId?: number) {
  TestBed.configureTestingModule({
    imports: [NotasSoPdfComponent],
    providers: [{ provide: PrevisioService, useValue: api }]
  });
  const fixture = TestBed.createComponent(NotasSoPdfComponent);
  if (empresaId) {
    fixture.componentRef.setInput('empresaId', empresaId);
  }
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { fixture, tela: fixture.nativeElement as HTMLElement };
}

describe('NotasSoPdfComponent', () => {
  it('sem notas em PDF, não mostra nada', async () => {
    const api = apiCom([]);
    const { tela } = await montar(api, 4);
    expect(api.obterNotasSoPdf).toHaveBeenCalledWith(4);
    expect(tela.querySelector('.card')).toBeNull();
  });

  it('lista a situação, o motivo e o número tirado da chave', async () => {
    const api = apiCom([
      nota(1, 'BuscandoNaSefaz', { numeroNota: '92001', cnpjFornecedor: '71295348000100', motivo: 'Ciência da operação registrada.' }),
      nota(2, 'SemChave', { motivo: 'Não achei a chave de acesso no PDF.', xmlPedidoEm: '2026-11-11T15:00:00Z' })
    ]);
    const { tela } = await montar(api);
    const linhas = tela.querySelectorAll('tbody tr');
    expect(linhas.length).toBe(2);
    expect(linhas[0].textContent).toContain('92001');
    expect(linhas[0].textContent).toContain('71.295.348/0001-00');
    expect(linhas[0].textContent).toContain('Buscando o XML na SEFAZ');
    expect(linhas[0].querySelector('.badge')?.classList.contains('badge-info')).toBe(true);
    expect(linhas[1].textContent).toContain('Sem chave de acesso');
    expect(linhas[1].textContent).toContain('XML pedido ao fornecedor em');
  });

  it('informar a chave envia o texto digitado e atualiza a lista', async () => {
    const api = apiCom([nota(2, 'SemChave')]);
    const { fixture, tela } = await montar(api);
    fixture.componentInstance.abrir(nota(2, 'SemChave'), 'chave');
    await fixture.whenStable();
    const campo = tela.querySelector('input[type=text]') as HTMLInputElement;
    campo.value = '3126 1171 2953';
    campo.dispatchEvent(new Event('input'));
    (tela.querySelector('.pv-so-pdf__botoes .btn-info') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.informarChaveNotaSoPdf).toHaveBeenCalledWith(2, '3126 1171 2953');
    expect(api.obterNotasSoPdf).toHaveBeenCalledTimes(2);
    expect(tela.querySelector('.alert-info')?.textContent).toContain('buscar o XML na SEFAZ');
  });

  it('mostra o motivo quando a API recusa', async () => {
    const api = apiCom([nota(2, 'SemChave')]);
    api.pedirXmlNotaSoPdf = vi.fn(() => throwError(() =>
      new HttpErrorResponse({ status: 400, error: 'O envio de e-mail não está configurado no servidor (seção Smtp).' })));
    const { fixture, tela } = await montar(api);
    fixture.componentInstance.pedirXml(nota(2, 'SemChave'));
    await fixture.whenStable();
    expect(tela.querySelector('.alert-danger')?.textContent).toContain('seção Smtp');
  });

  it('já lancei no Bling manda a observação', async () => {
    const api = apiCom([nota(3, 'NaoResolvida')]);
    const { fixture, tela } = await montar(api);
    expect(tela.querySelector('.badge')?.classList.contains('badge-danger')).toBe(true);
    fixture.componentInstance.abrir(nota(3, 'NaoResolvida'), 'lancada');
    await fixture.whenStable();
    const campo = tela.querySelector('input[type=text]') as HTMLInputElement;
    campo.value = 'conta 4512';
    campo.dispatchEvent(new Event('input'));
    (tela.querySelector('.pv-so-pdf__botoes .btn-info') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.marcarNotaSoPdfLancada).toHaveBeenCalledWith(3, 'conta 4512');
  });
});
