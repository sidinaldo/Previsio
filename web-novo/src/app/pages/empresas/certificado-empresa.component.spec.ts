import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { Empresa } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CertificadoEmpresaComponent } from './certificado-empresa.component';

const EMPRESA: Empresa = {
  id: 5, nome: 'Auto Center Centro', cnpj: '11222333000262', blingConectado: true, blingClientId: null,
  blingClientSecretCadastrado: false, origemAplicativoBling: 'Padrao', emailProprio: false, imapHost: '', imapPorta: 993,
  imapSsl: true, imapUsuario: '', imapSenhaCadastrada: false, ultimaSincronizacaoEm: null, ativo: true
};

async function montar(empresa: Empresa, api: Record<string, unknown>) {
  TestBed.configureTestingModule({
    imports: [CertificadoEmpresaComponent],
    providers: [{ provide: PrevisioService, useValue: api }]
  });
  const fixture = TestBed.createComponent(CertificadoEmpresaComponent);
  fixture.componentRef.setInput('empresa', empresa);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { fixture, tela: fixture.nativeElement as HTMLElement };
}

describe('CertificadoEmpresaComponent', () => {
  it('envia o arquivo, a senha e a UF e avisa quem usa', async () => {
    const cadastrado: Empresa = { ...EMPRESA, certificadoCadastrado: true, certificadoTitular: 'AUTO CENTER LTDA (11.222.333/0001-81)',
      certificadoValidoAte: '2027-05-10T00:00:00Z', uf: 'RN' };
    const api = { salvarCertificado: vi.fn(() => of(cadastrado)), removerCertificado: vi.fn() };
    const { fixture, tela } = await montar(EMPRESA, api);
    const alterada = vi.fn();
    fixture.componentInstance.alterada.subscribe(alterada);

    const arquivo = new File(['pfx'], 'certificado.pfx');
    fixture.componentInstance.arquivo.set(arquivo);
    fixture.componentInstance.senha.set('1234');
    fixture.componentInstance.uf.set('RN');
    await fixture.whenStable();
    (tela.querySelector('.pv-certificado__enviar') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(api.salvarCertificado).toHaveBeenCalledWith(5, arquivo, '1234', 'RN');
    expect(alterada).toHaveBeenCalledWith(cadastrado);
    expect(tela.querySelector('[role=status]')?.textContent).toContain('Certificado cadastrado');
  });

  it('sem UF não envia; recusa da API aparece embaixo', async () => {
    const api = {
      salvarCertificado: vi.fn(() => throwError(() => new HttpErrorResponse({ status: 400, error: 'Não consegui abrir o certificado: confira o arquivo .pfx e a senha.' }))),
      removerCertificado: vi.fn()
    };
    const { fixture, tela } = await montar(EMPRESA, api);
    fixture.componentInstance.arquivo.set(new File(['pfx'], 'c.pfx'));
    fixture.componentInstance.enviar();
    await fixture.whenStable();
    expect(api.salvarCertificado).not.toHaveBeenCalled();
    expect(tela.querySelector('[role=alert]')?.textContent).toContain('Escolha a UF');

    fixture.componentInstance.uf.set('SP');
    fixture.componentInstance.enviar();
    await fixture.whenStable();
    expect(tela.querySelector('[role=alert]')?.textContent).toContain('confira o arquivo .pfx e a senha');
  });

  it('mostra o certificado atual e remove', async () => {
    const cadastrado: Empresa = { ...EMPRESA, certificadoCadastrado: true, certificadoTitular: 'AUTO CENTER LTDA (11.222.333/0001-81)',
      certificadoValidoAte: '2027-05-10T12:00:00Z', uf: 'RN' };
    const api = { salvarCertificado: vi.fn(), removerCertificado: vi.fn(() => of(EMPRESA)) };
    const { fixture, tela } = await montar(cadastrado, api);
    expect(tela.querySelector('.pv-certificado__atual')?.textContent).toContain('AUTO CENTER LTDA');
    expect(tela.querySelector('.pv-certificado__atual')?.textContent).toContain('10/05/2027');
    expect((tela.querySelector('select') as HTMLSelectElement).value).toBe('RN');

    (tela.querySelector('.pv-certificado__atual button') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.removerCertificado).toHaveBeenCalledWith(5);
  });

  it('mostra a última busca na SEFAZ e liga/desliga a busca completa', async () => {
    const ligada: Empresa = { ...EMPRESA, sefazBuscaAtiva: true, sefazUltimaBuscaEm: '2026-11-14T13:00:00Z', sefazSituacao: '2 notas novas.' };
    const desligada: Empresa = { ...ligada, sefazBuscaAtiva: false };
    const api = { alterarBuscaSefaz: vi.fn(() => of(desligada)) };
    const { fixture, tela } = await montar(ligada, api);
    const alterada = vi.fn();
    fixture.componentInstance.alterada.subscribe(alterada);

    expect(tela.querySelector('.pv-certificado__situacao')?.textContent).toContain('2 notas novas.');
    const caixa = tela.querySelector('#sefaz-busca-5') as HTMLInputElement;
    expect(caixa.checked).toBe(true);
    caixa.checked = false;
    caixa.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(api.alterarBuscaSefaz).toHaveBeenCalledWith(5, false);
    expect(alterada).toHaveBeenCalledWith(desligada);
    expect(tela.querySelector('.pv-certificado__ok')?.textContent).toContain('desligada');
  });

  it('erro ao ligar a busca volta a caixa e mostra o motivo', async () => {
    const api = { alterarBuscaSefaz: vi.fn(() => throwError(() => new HttpErrorResponse({ status: 404, error: 'Empresa não encontrada.' }))) };
    const { fixture, tela } = await montar({ ...EMPRESA, sefazBuscaAtiva: false }, api);
    const caixa = tela.querySelector('#sefaz-busca-5') as HTMLInputElement;
    caixa.checked = true;
    caixa.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(caixa.checked).toBe(false);
    expect(tela.querySelector('.pv-certificado__erro')?.textContent).toContain('Empresa não encontrada.');
  });
});
