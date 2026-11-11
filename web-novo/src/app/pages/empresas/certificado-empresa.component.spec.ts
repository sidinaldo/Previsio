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
});
