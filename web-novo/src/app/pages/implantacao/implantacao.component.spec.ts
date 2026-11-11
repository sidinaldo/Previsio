import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { ImplantacaoComponent } from './implantacao.component';
import { Cliente, Empresa } from '../../core/previsio.models';

const CLIENTE: Cliente = {
  id: 7, nome: 'Rede Alfa', imapHost: 'imap.gmail.com', imapPorta: 993, imapSsl: true,
  imapUsuario: 'notas@alfa.com.br', blingClientId: null, blingClientSecretCadastrado: false, ativo: true
};

function empresa(id: number, conectada: boolean): Empresa {
  return {
    id: id, nome: 'Loja ' + id, cnpj: '1122233300018' + id, blingConectado: conectada, blingClientId: null,
    blingClientSecretCadastrado: false, origemAplicativoBling: 'Padrao', emailProprio: false, imapHost: '',
    imapPorta: 993, imapSsl: true, imapUsuario: '', imapSenhaCadastrada: false, ultimaSincronizacaoEm: null, ativo: true
  };
}

function montar(id: string | null) {
  TestBed.configureTestingModule({
    imports: [ImplantacaoComponent],
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id === null ? {} : { id: id }) } } }
    ]
  });
  const fixture = TestBed.createComponent(ImplantacaoComponent);
  fixture.autoDetectChanges();
  return { fixture, http: TestBed.inject(HttpTestingController), tela: fixture.nativeElement as HTMLElement };
}

describe('ImplantacaoComponent', () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('previsio.sessao', JSON.stringify({
      token: 't', expiraEm: '2099-01-01T00:00:00Z',
      usuario: { id: 1, clienteId: null, nome: 'Admin', email: 'a@b.c', perfil: 'Admin', ativo: true, ultimoAcessoEm: null }
    }));
  });

  it('cliente novo: abre na rede, com as outras etapas fechadas e o cadastro travado até testar a caixa', async () => {
    const { fixture, tela } = montar(null);
    await fixture.whenStable();

    const botoes = tela.querySelectorAll<HTMLButtonElement>('.pv-etapas__botao');
    expect(botoes.length).toBe(5);
    expect(botoes[0].disabled).toBe(false);
    expect(botoes[1].disabled).toBe(true);
    expect(tela.querySelector('.pv-card__titulo')!.textContent).toContain('1. Rede e caixa de e-mail');

    const cadastrar = tela.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(cadastrar.textContent).toContain('Cadastrar rede e continuar');
    expect(cadastrar.disabled).toBe(true);
  });

  it('retomando: vai direto para a primeira etapa que falta (Bling com uma loja desconectada)', async () => {
    const { fixture, http, tela } = montar('7');
    http.expectOne(r => r.url.endsWith('/clientes/7')).flush(CLIENTE);
    http.expectOne(r => r.url.endsWith('/clientes/7/empresas')).flush([empresa(1, true), empresa(2, false)]);
    http.expectOne(r => r.url.endsWith('/usuarios')).flush([]);
    http.expectOne(r => r.url.endsWith('/clientes/7/notas')).flush({ itens: [], total: 0, pagina: 1, tamanhoPagina: 20 });
    await fixture.whenStable();

    expect(tela.querySelector('.pv-card__titulo')!.textContent).toContain('3. Conectar o Bling');
    expect(tela.querySelector('.pv-card__descricao')!.textContent).toContain('Falta conectar 1 empresa');
    expect(tela.querySelectorAll('.pv-etapas__item--concluida').length).toBe(2);
    // Só a loja desconectada tem o botão
    expect(tela.querySelectorAll('tbody button').length).toBe(1);
  });
});
