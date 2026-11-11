import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { EmpresasComponent } from './empresas.component';
import { Empresa } from '../../core/previsio.models';
import { PREVISIO_CONFIG } from '../../core/previsio.config';
import { SessaoService } from '../../core/sessao.service';

const API = PREVISIO_CONFIG.apiUrl;

const EMPRESAS: Empresa[] = [
  { id: 1, nome: 'Loja Centro', cnpj: '11222333000181', blingConectado: true, blingClientId: null, blingClientSecretCadastrado: false,
    ultimaSincronizacaoEm: null, ativo: true },
  { id: 2, nome: 'Loja Norte', cnpj: '11222333000262', blingConectado: false, blingClientId: null, blingClientSecretCadastrado: false,
    ultimaSincronizacaoEm: null, ativo: true }
];

async function montar() {
  localStorage.clear();
  localStorage.setItem('previsio.sessao', JSON.stringify({
    token: 't', expiraEm: '2099-01-01T00:00:00Z',
    usuario: { id: 1, clienteId: null, nome: 'Admin', email: 'a@b.c', perfil: 'Admin', ativo: true, ultimoAcessoEm: null }
  }));
  TestBed.configureTestingModule({
    imports: [EmpresasComponent],
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
    ]
  });
  TestBed.inject(SessaoService).escolherCliente(7);
  const fixture = TestBed.createComponent(EmpresasComponent);
  fixture.autoDetectChanges();
  const api = TestBed.inject(HttpTestingController);
  api.expectOne(`${API}/clientes/7/empresas`).flush(EMPRESAS);
  await fixture.whenStable();
  return { fixture, api, tela: fixture.nativeElement as HTMLElement };
}

describe('EmpresasComponent: edição no formulário do topo', () => {
  it('a tabela não tem campos de edição; Editar preenche o formulário do topo', async () => {
    const { fixture, tela } = await montar();
    expect(tela.querySelector('table input')).toBeNull();

    fixture.componentInstance.editar(EMPRESAS[1]);
    await fixture.whenStable();

    expect(tela.querySelector('.pv-card__titulo')!.textContent).toContain('Editar empresa');
    expect((tela.querySelector('#previsio-nome') as HTMLInputElement).value).toBe('Loja Norte');
    expect((tela.querySelector('#previsio-cnpj') as HTMLInputElement).readOnly).toBe(true);
    expect(tela.querySelector('table input')).toBeNull();
    expect(tela.querySelector('tr.pv-linha--editando')!.textContent).toContain('Loja Norte');
  });

  it('Salvar altera a empresa escolhida (PUT) e volta para o cadastro de uma nova', async () => {
    const { fixture, api, tela } = await montar();
    const componente = fixture.componentInstance;
    componente.editar(EMPRESAS[1]);
    componente.nome.set('Loja Norte Shopping');
    componente.salvar();

    const chamada = api.expectOne(`${API}/empresas/2`);
    expect(chamada.request.method).toBe('PUT');
    expect(chamada.request.body).toEqual({ nome: 'Loja Norte Shopping', blingClientId: '' });
    chamada.flush({ ...EMPRESAS[1], nome: 'Loja Norte Shopping' });
    await fixture.whenStable();

    expect(componente.editando()).toBeNull();
    expect(componente.nome()).toBe('');
    expect(tela.querySelector('.pv-card__titulo')!.textContent).toContain('Nova empresa');
    expect(tela.textContent).toContain('Loja Norte Shopping');
    expect(tela.textContent).toContain('Empresa alterada.');
  });

  it('Cancelar volta para o cadastro sem chamar a API', async () => {
    const { fixture, api } = await montar();
    const componente = fixture.componentInstance;
    componente.editar(EMPRESAS[0]);
    componente.cancelarEdicao();
    await fixture.whenStable();

    expect(componente.editando()).toBeNull();
    expect(componente.nome()).toBe('');
    api.verify();
  });

  it('sem edição, o formulário continua cadastrando (POST)', async () => {
    const { fixture, api } = await montar();
    const componente = fixture.componentInstance;
    componente.nome.set('Loja Sul');
    componente.cnpj.set('11.222.333/0004-05');
    componente.salvar();

    const chamada = api.expectOne(`${API}/clientes/7/empresas`);
    expect(chamada.request.method).toBe('POST');
    expect(chamada.request.body).toEqual({ nome: 'Loja Sul', cnpj: '11222333000405' });
  });
});
