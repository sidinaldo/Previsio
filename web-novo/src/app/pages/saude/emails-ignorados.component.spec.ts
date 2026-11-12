import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { EmailIgnorado } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { EmailsIgnoradosComponent } from './emails-ignorados.component';

function email(id: number, temPdf: boolean): EmailIgnorado {
  return {
    id: id, clienteId: 7, clienteNome: 'Rede Auto Center', caixa: 'Caixa da rede', remetente: 'Alfa <nf@alfa.com.br>',
    assunto: 'Documento ' + id, enviadoEm: null, lidoEm: '2026-11-11T12:00:00Z', anexos: temPdf ? 'doc.pdf' : 'foto.jpg',
    motivo: temPdf ? 'PDF não parece nota fiscal' : 'Os anexos não são XML, ZIP com XML nem PDF.', temPdf: temPdf,
    tratadoEm: null, tratadoPor: null, resultadoTratamento: null
  };
}

async function montar() {
  const api = {
    obterEmailsIgnorados: vi.fn((_: number | null, __: number) => of([email(1, true), email(2, false)])),
    baixarPdfEmailIgnorado: vi.fn(),
    tratarEmailIgnoradoComoNota: vi.fn(() => of({
      mensagem: 'Virou pendência em Notas pendentes, sem chave de acesso.',
      email: { ...email(1, true), tratadoEm: '2026-11-11T13:00:00Z', tratadoPor: 'admin', resultadoTratamento: 'Virou pendência' }
    }))
  };
  TestBed.configureTestingModule({ imports: [EmailsIgnoradosComponent], providers: [{ provide: PrevisioService, useValue: api }] });
  const fixture = TestBed.createComponent(EmailsIgnoradosComponent);
  fixture.componentRef.setInput('redes', [{ clienteId: 7, nome: 'Rede Auto Center' }]);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { fixture, api, tela: fixture.nativeElement as HTMLElement };
}

describe('EmailsIgnoradosComponent', () => {
  it('lista os dos últimos 7 dias com motivo; só o com PDF tem ações', async () => {
    const { tela, api } = await montar();
    expect(api.obterEmailsIgnorados).toHaveBeenCalledWith(null, 7);
    const linhas = tela.querySelectorAll('tbody tr');
    expect(linhas.length).toBe(2);
    expect(linhas[0].textContent).toContain('PDF não parece nota fiscal');
    expect(linhas[0].textContent).toContain('Tratar como nota em PDF');
    expect(linhas[1].textContent).not.toContain('Tratar como nota em PDF');
  });

  it('filtrar por rede e período recarrega', async () => {
    const { fixture, tela, api } = await montar();
    const selects = tela.querySelectorAll('select');
    (selects[0] as HTMLSelectElement).value = '7';
    selects[0].dispatchEvent(new Event('change'));
    (selects[1] as HTMLSelectElement).value = '30';
    selects[1].dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(api.obterEmailsIgnorados).toHaveBeenLastCalledWith(7, 30);
  });

  it('tratar como nota mostra o resultado e tira o botão', async () => {
    const { fixture, tela, api } = await montar();
    (Array.from(tela.querySelectorAll('button')).find(b => b.textContent?.includes('Tratar')) as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.tratarEmailIgnoradoComoNota).toHaveBeenCalledWith(1);
    expect(tela.querySelector('.alert-success')?.textContent).toContain('Virou pendência');
    expect(tela.querySelector('tbody tr')?.textContent).toContain('Tratado');
    expect(tela.textContent).not.toContain('Tratar como nota em PDF');
  });
});
