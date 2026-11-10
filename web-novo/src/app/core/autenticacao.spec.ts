import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';

import { autenticacaoInterceptor } from './autenticacao.interceptor';
import { exigirLogin, somenteAdmin, somenteSemLogin } from './autenticacao.guards';
import { PREVISIO_CONFIG } from './previsio.config';
import { RespostaLogin } from './previsio.models';
import { SessaoService } from './sessao.service';

const API = PREVISIO_CONFIG.apiUrl;

function resposta(perfil: string, clienteId: number | null, minutosAteVencer: number): RespostaLogin {
  return {
    token: 'token-de-teste',
    expiraEm: new Date(Date.now() + minutosAteVencer * 60000).toISOString(),
    usuario: {
      id: 7,
      clienteId: clienteId,
      nome: 'Fulano',
      email: 'fulano@exemplo.com',
      perfil: perfil,
      ativo: true,
      ultimoAcessoEm: null
    }
  };
}

function configurar(): { sessao: SessaoService; http: HttpClient; api: HttpTestingController; navegar: ReturnType<typeof vi.fn> } {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(withInterceptors([autenticacaoInterceptor])),
      provideHttpClientTesting()
    ]
  });
  const router = TestBed.inject(Router);
  const navegar = vi.fn().mockResolvedValue(true);
  router.navigate = navegar;
  return {
    sessao: TestBed.inject(SessaoService),
    http: TestBed.inject(HttpClient),
    api: TestBed.inject(HttpTestingController),
    navegar: navegar
  };
}

function entrar(sessao: SessaoService, api: HttpTestingController, dados: RespostaLogin): void {
  sessao.entrar('fulano@exemplo.com', 'senha-boa-123').subscribe();
  api.expectOne(`${API}/auth/login`).flush(dados);
}

describe('SessaoService', () => {
  beforeEach(() => localStorage.clear());

  it('guarda o login e usa o cliente do próprio usuário', () => {
    const { sessao, api } = configurar();
    entrar(sessao, api, resposta('Cliente', 3, 60));

    expect(sessao.estaLogado()).toBe(true);
    expect(sessao.token()).toBe('token-de-teste');
    expect(sessao.ehAdmin()).toBe(false);
    expect(sessao.clienteId()).toBe(3);
    expect(localStorage.getItem('previsio.sessao')).toContain('token-de-teste');
  });

  it('admin usa o cliente escolhido e a escolha fica guardada', () => {
    const { sessao, api } = configurar();
    entrar(sessao, api, resposta('Admin', null, 60));

    expect(sessao.ehAdmin()).toBe(true);
    expect(sessao.clienteId()).toBe(null);
    sessao.escolherCliente(5);
    expect(sessao.clienteId()).toBe(5);
    expect(localStorage.getItem('previsio.cliente')).toBe('5');
  });

  it('cliente comum ignora a escolha guardada pelo admin', () => {
    localStorage.setItem('previsio.cliente', '9');
    const { sessao, api } = configurar();
    entrar(sessao, api, resposta('Cliente', 3, 60));

    expect(sessao.clienteId()).toBe(3);
  });

  it('descarta login vencido salvo no navegador', () => {
    localStorage.setItem('previsio.sessao', JSON.stringify(resposta('Cliente', 3, -1)));
    const { sessao } = configurar();

    expect(sessao.estaLogado()).toBe(false);
    expect(sessao.usuario()).toBe(null);
    expect(localStorage.getItem('previsio.sessao')).toBe(null);
  });

  it('descarta login salvo corrompido', () => {
    localStorage.setItem('previsio.sessao', '{isso não é json');
    const { sessao } = configurar();

    expect(sessao.estaLogado()).toBe(false);
    expect(localStorage.getItem('previsio.sessao')).toBe(null);
  });

  it('sair limpa o login e volta para a tela de entrada', () => {
    const { sessao, api, navegar } = configurar();
    entrar(sessao, api, resposta('Cliente', 3, 60));

    sessao.sair();

    expect(sessao.estaLogado()).toBe(false);
    expect(localStorage.getItem('previsio.sessao')).toBe(null);
    expect(navegar).toHaveBeenCalledWith(['/pages/login']);
  });
});

describe('autenticacaoInterceptor', () => {
  beforeEach(() => localStorage.clear());

  it('envia o token nas chamadas à API', () => {
    const { sessao, http, api } = configurar();
    entrar(sessao, api, resposta('Cliente', 3, 60));

    http.get(`${API}/clientes/3/painel`).subscribe();
    const chamada = api.expectOne(`${API}/clientes/3/painel`);
    expect(chamada.request.headers.get('Authorization')).toBe('Bearer token-de-teste');
    chamada.flush({});
  });

  it('não envia o token para outros endereços nem no login', () => {
    const { sessao, http, api } = configurar();
    entrar(sessao, api, resposta('Cliente', 3, 60));

    http.get('https://fonts.googleapis.com/css').subscribe();
    const externa = api.expectOne('https://fonts.googleapis.com/css');
    expect(externa.request.headers.has('Authorization')).toBe(false);
    externa.flush('');

    sessao.entrar('outro@exemplo.com', 'x').subscribe({ error: () => undefined });
    const login = api.expectOne(`${API}/auth/login`);
    expect(login.request.headers.has('Authorization')).toBe(false);
    login.flush('E-mail ou senha inválidos.', { status: 401, statusText: 'Unauthorized' });
  });

  it('401 da API encerra o login e avisa que a sessão terminou', () => {
    const { sessao, http, api, navegar } = configurar();
    entrar(sessao, api, resposta('Cliente', 3, 60));

    let status = 0;
    http.get(`${API}/clientes/3/painel`).subscribe({ error: erro => (status = erro.status) });
    api.expectOne(`${API}/clientes/3/painel`).flush('', { status: 401, statusText: 'Unauthorized' });

    expect(status).toBe(401);
    expect(sessao.estaLogado()).toBe(false);
    expect(navegar).toHaveBeenCalledWith(['/pages/login'], { queryParams: { motivo: 'expirou' } });
  });

  it('401 no login (senha errada) não mexe no login atual', () => {
    const { sessao, api, navegar } = configurar();
    entrar(sessao, api, resposta('Cliente', 3, 60));

    sessao.entrar('fulano@exemplo.com', 'errada').subscribe({ error: () => undefined });
    api.expectOne(`${API}/auth/login`).flush('', { status: 401, statusText: 'Unauthorized' });

    expect(sessao.estaLogado()).toBe(true);
    expect(navegar).not.toHaveBeenCalled();
  });
});

describe('guardas de rota', () => {
  beforeEach(() => localStorage.clear());

  const rota = {} as ActivatedRouteSnapshot;
  const estado = { url: '/pages/empresas' } as RouterStateSnapshot;

  it('sem login, manda para a tela de entrada guardando a tela pedida', () => {
    configurar();
    const resultado = TestBed.runInInjectionContext(() => exigirLogin(rota, estado));

    expect(resultado instanceof UrlTree).toBe(true);
    expect(TestBed.inject(Router).serializeUrl(resultado as UrlTree)).toBe('/pages/login?retorno=%2Fpages%2Fempresas');
  });

  it('com login, libera as telas e pula a tela de entrada', () => {
    const { sessao, api } = configurar();
    entrar(sessao, api, resposta('Cliente', 3, 60));

    expect(TestBed.runInInjectionContext(() => exigirLogin(rota, estado))).toBe(true);
    const pulo = TestBed.runInInjectionContext(() => somenteSemLogin(rota, estado));
    expect(TestBed.inject(Router).serializeUrl(pulo as UrlTree)).toBe('/pages/painel');
  });

  it('tela só de admin libera o admin', () => {
    const { sessao, api } = configurar();
    entrar(sessao, api, resposta('Admin', null, 60));

    expect(TestBed.runInInjectionContext(() => somenteAdmin(rota, estado))).toBe(true);
  });

  it('tela só de admin manda o usuário de cliente para o painel', () => {
    const { sessao, api } = configurar();
    entrar(sessao, api, resposta('Cliente', 3, 60));

    const resultado = TestBed.runInInjectionContext(() => somenteAdmin(rota, estado));
    expect(TestBed.inject(Router).serializeUrl(resultado as UrlTree)).toBe('/pages/painel');
  });
});
