import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { autenticacaoInterceptor } from './autenticacao.interceptor';
import { ClientesService } from './clientes.service';
import { PREVISIO_CONFIG } from './previsio.config';
import { Cliente, NovoCliente, RespostaLogin } from './previsio.models';
import { SessaoService } from './sessao.service';

const API = PREVISIO_CONFIG.apiUrl;

function cliente(id: number, nome: string): Cliente {
  return { id: id, nome: nome, imapHost: 'imap.gmail.com', imapPorta: 993, imapSsl: true, imapUsuario: 'notas@exemplo.com', ativo: true };
}

function configurarComAdmin(): { clientes: ClientesService; sessao: SessaoService; api: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(withInterceptors([autenticacaoInterceptor])),
      provideHttpClientTesting()
    ]
  });
  const sessao = TestBed.inject(SessaoService);
  const api = TestBed.inject(HttpTestingController);

  const login: RespostaLogin = {
    token: 'token-do-admin',
    expiraEm: new Date(Date.now() + 3600000).toISOString(),
    usuario: { id: 1, clienteId: null, nome: 'Admin', email: 'admin@exemplo.com', perfil: 'Admin', ativo: true, ultimoAcessoEm: null }
  };
  sessao.entrar('admin@exemplo.com', 'senha-boa-123').subscribe();
  api.expectOne(`${API}/auth/login`).flush(login);

  return { clientes: TestBed.inject(ClientesService), sessao: sessao, api: api };
}

describe('ClientesService', () => {
  beforeEach(() => localStorage.clear());

  it('sem escolha guardada, passa a exibir o primeiro cliente', () => {
    const { clientes, sessao, api } = configurarComAdmin();

    clientes.carregar();
    api.expectOne(`${API}/clientes`).flush([cliente(4, 'Rede Alfa'), cliente(9, 'Rede Beta')]);

    expect(clientes.carregada()).toBe(true);
    expect(clientes.lista().length).toBe(2);
    expect(sessao.clienteId()).toBe(4);
  });

  it('mantém o cliente escolhido antes, se ele ainda existe', () => {
    localStorage.setItem('previsio.cliente', '9');
    const { clientes, sessao, api } = configurarComAdmin();

    clientes.carregar();
    api.expectOne(`${API}/clientes`).flush([cliente(4, 'Rede Alfa'), cliente(9, 'Rede Beta')]);

    expect(sessao.clienteId()).toBe(9);
  });

  it('nenhum cliente cadastrado: lista vazia e nada escolhido', () => {
    const { clientes, sessao, api } = configurarComAdmin();

    clientes.carregar();
    api.expectOne(`${API}/clientes`).flush([]);

    expect(clientes.carregada()).toBe(true);
    expect(clientes.lista().length).toBe(0);
    expect(clientes.erro()).toBe('');
    expect(sessao.clienteId()).toBe(null);
  });

  it('falha ao carregar avisa o erro', () => {
    const { clientes, api } = configurarComAdmin();

    clientes.carregar();
    api.expectOne(`${API}/clientes`).flush('', { status: 500, statusText: 'Server Error' });

    expect(clientes.carregada()).toBe(true);
    expect(clientes.erro()).toContain('Não foi possível carregar');
  });

  it('cadastrar envia os dados da caixa, põe o cliente na lista em ordem e passa a exibi-lo', () => {
    const { clientes, sessao, api } = configurarComAdmin();
    clientes.carregar();
    api.expectOne(`${API}/clientes`).flush([cliente(4, 'Rede Beta')]);

    const dados: NovoCliente = {
      nome: 'Rede Alfa',
      imapHost: 'imap.gmail.com',
      imapPorta: 993,
      imapSsl: true,
      imapUsuario: 'notas@alfa.com.br',
      imapSenha: 'senha-de-app'
    };
    let criado: Cliente | null = null;
    clientes.criar(dados).subscribe(resposta => (criado = resposta));

    const chamada = api.expectOne(`${API}/clientes`);
    expect(chamada.request.method).toBe('POST');
    expect(chamada.request.body).toEqual(dados);
    expect(chamada.request.headers.get('Authorization')).toBe('Bearer token-do-admin');
    chamada.flush(cliente(7, 'Rede Alfa'));

    expect(criado).not.toBe(null);
    expect(clientes.lista().map(c => c.nome)).toEqual(['Rede Alfa', 'Rede Beta']);
    expect(sessao.clienteId()).toBe(7);
  });
});
