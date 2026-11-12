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
  { id: 1, nome: 'Loja Centro', cnpj: '11222333000181', blingConectado: true, blingClientId: null, blingClientSecretCadastrado: false, origemAplicativoBling: 'Padrao', emailProprio: false,
    imapHost: '', imapPorta: 993, imapSsl: true, imapUsuario: '', imapSenhaCadastrada: false,
    ultimaSincronizacaoEm: null, ativo: true },
  { id: 2, nome: 'Loja Norte', cnpj: '11222333000262', blingConectado: false, blingClientId: null, blingClientSecretCadastrado: false, origemAplicativoBling: 'Padrao', emailProprio: false,
    imapHost: '', imapPorta: 993, imapSsl: true, imapUsuario: '', imapSenhaCadastrada: false,
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
  api.expectOne(`${API}/clientes/7`).flush({
    id: 7, nome: 'Rede', imapHost: 'imap.gmail.com', imapPorta: 993, imapSsl: true, imapUsuario: 'notas@rede.com.br',
    blingClientId: null, blingClientSecretCadastrado: false, ativo: true
  });
  // Cards do fim da tela: conferência com o Bling, avisos de vencimento e resumo por e-mail
  api.expectOne(`${API}/clientes/7/conferencia-bling`).flush({ conferirAntesDeLancar: true, exigeNumeroNota: false, toleranciaValor: 0.1, toleranciaDias: 0, fecharPendentes: true });
  api.expectOne(`${API}/clientes/7/alertas-vencimento`).flush({ avisoVencidasDias: 1, alertaDiarioAtivo: true, alertaDiarioHora: 7 });
  api.expectOne(`${API}/clientes/7/resumo-semanal`).flush({ ativo: true, dia: 1, hora: 7, ultimoEnvioEm: null, destinatarios: 1 });
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

  it('Salvar altera a empresa escolhida (PUT) e fecha o formulário', async () => {
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
    // Depois de salvar o formulário fecha e a tela volta para a lista
    expect(tela.querySelector('#previsio-nome')).toBeNull();
    expect(tela.textContent).toContain('Loja Norte Shopping');
    expect(tela.textContent).toContain('Empresa salva.');
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

describe('EmpresasComponent: e-mail próprio da empresa', () => {
  it('cadastro com e-mail próprio envia servidor, porta, SSL, e-mail e senha', async () => {
    const { fixture, api } = await montar();
    const componente = fixture.componentInstance;
    componente.nome.set('Loja Sul');
    componente.cnpj.set('11222333000405');
    componente.imapHost.set('imap.gmail.com');
    componente.imapUsuario.set('sul@rede.com.br');
    componente.imapSenha.set('senha-de-app');
    componente.salvar();

    const chamada = api.expectOne(`${API}/clientes/7/empresas`);
    expect(chamada.request.body).toEqual({
      nome: 'Loja Sul', cnpj: '11222333000405',
      imapHost: 'imap.gmail.com', imapPorta: 993, imapSsl: true, imapUsuario: 'sul@rede.com.br', imapSenha: 'senha-de-app'
    });
  });

  it('servidor sem e-mail da caixa não chama a API', async () => {
    const { fixture, api, tela } = await montar();
    const componente = fixture.componentInstance;
    componente.nome.set('Loja Sul');
    componente.cnpj.set('11222333000405');
    componente.imapHost.set('imap.gmail.com');
    componente.salvar();
    await fixture.whenStable();

    api.expectNone(`${API}/clientes/7/empresas`);
    expect(tela.textContent).toContain('Informe o e-mail da caixa própria');
  });

  it('na edição, a senha em branco mantém a atual da mesma caixa', async () => {
    const { fixture, api } = await montar();
    const componente = fixture.componentInstance;
    const comEmail: Empresa = {
      ...EMPRESAS[0], emailProprio: true, imapHost: 'imap.gmail.com', imapUsuario: 'centro@rede.com.br', imapSenhaCadastrada: true
    };
    componente.editar(comEmail);
    componente.salvar();

    const chamada = api.expectOne(`${API}/empresas/1`);
    expect(chamada.request.body).toEqual({
      nome: 'Loja Centro', blingClientId: '',
      imapHost: 'imap.gmail.com', imapPorta: 993, imapSsl: true, imapUsuario: 'centro@rede.com.br'
    });
  });

  it('a tabela mostra de onde vêm o e-mail e o aplicativo do Bling', async () => {
    const { tela } = await montar();
    expect(tela.textContent).toContain('Caixa da rede');
    expect(tela.textContent).toContain('Aplicativo padrão');
  });
});

describe('EmpresasComponent: conectar o Bling numa janela separada', () => {
  afterEach(() => vi.restoreAllMocks());

  it('abre a janela no clique e manda ela para o Bling, sem sair do Previsio', async () => {
    const { fixture, api, tela } = await montar();
    const janela = { closed: false, location: { href: '' }, focus: vi.fn(), close: vi.fn() };
    const abrir = vi.spyOn(window, 'open').mockReturnValue(janela as unknown as Window);

    fixture.componentInstance.conectarBling(EMPRESAS[1]);
    expect(abrir).toHaveBeenCalledWith('', 'previsio-bling', expect.stringContaining('popup'));

    api.expectOne(`${API}/empresas/2/bling/conectar`).flush({ url: 'https://www.bling.com.br/Api/v3/oauth/authorize?x=1' });
    await fixture.whenStable();

    expect(janela.location.href).toBe('https://www.bling.com.br/Api/v3/oauth/authorize?x=1');
    expect(tela.textContent).toContain('Entre no Bling e autorize o acesso na janela que abriu');
  });

  it('se a API recusar, fecha a janela e mostra o motivo', async () => {
    const { fixture, api, tela } = await montar();
    const janela = { closed: false, location: { href: '' }, focus: vi.fn(), close: vi.fn() };
    vi.spyOn(window, 'open').mockReturnValue(janela as unknown as Window);

    fixture.componentInstance.conectarBling(EMPRESAS[1]);
    api.expectOne(`${API}/empresas/2/bling/conectar`).flush(
      'Cadastre o Client ID e o Client Secret do aplicativo do Bling desta empresa antes de conectar.',
      { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    expect(janela.close).toHaveBeenCalled();
    expect(tela.textContent).toContain('Cadastre o Client ID');
  });

  it('quando a janela avisa que conectou, mostra a mensagem e recarrega a lista', async () => {
    const { fixture, api, tela } = await montar();

    const canal = new BroadcastChannel('previsio-bling');
    canal.postMessage('conectado');
    canal.close();
    await new Promise(resolve => setTimeout(resolve, 20));

    api.expectOne(`${API}/clientes/7/empresas`).flush(EMPRESAS);
    await fixture.whenStable();
    expect(tela.textContent).toContain('Bling conectado');
  });
});

describe('EmpresasComponent: coluna E-mail', () => {
  it('mostra a caixa da rede para quem não tem caixa própria e a própria para quem tem', async () => {
    const { fixture, api, tela } = await montar();
    const linhas = tela.querySelectorAll('tbody tr');
    expect(linhas[0].textContent).toContain('notas@rede.com.br');
    expect(linhas[0].textContent).toContain('Caixa da rede');

    const comCaixaPropria: Empresa[] = [{ ...EMPRESAS[0], emailProprio: true, imapHost: 'imap.gmail.com', imapUsuario: 'filial@loja.com.br' }, EMPRESAS[1]];
    fixture.componentInstance.carregar();
    api.expectOne(`${API}/clientes/7/empresas`).flush(comCaixaPropria);
    await fixture.whenStable();
    const primeira = tela.querySelectorAll('tbody tr')[0];
    expect(primeira.textContent).toContain('filial@loja.com.br');
    expect(primeira.textContent).toContain('Caixa própria');
  });
});

describe('EmpresasComponent: formulário fechado por padrão', () => {
  it('com empresas cadastradas, abre só em "Nova empresa" e fecha em Cancelar', async () => {
    const { fixture, tela } = await montar();
    expect(tela.querySelector('#previsio-nome')).toBeNull();

    const botao = Array.from(tela.querySelectorAll<HTMLButtonElement>('.pv-pagina__acoes button'))
      .find(b => b.textContent!.includes('Nova empresa'))!;
    botao.click();
    await fixture.whenStable();
    expect(tela.querySelector('#previsio-nome')).not.toBeNull();
    expect(tela.querySelector('.pv-opcional[open]')).toBeNull();

    fixture.componentInstance.cancelarEdicao();
    await fixture.whenStable();
    expect(tela.querySelector('#previsio-nome')).toBeNull();
  });

  it('editar empresa com caixa própria abre o bloco do e-mail', async () => {
    const { fixture, tela } = await montar();
    fixture.componentInstance.editar({ ...EMPRESAS[0], emailProprio: true, imapHost: 'imap.gmail.com', imapUsuario: 'c@r.com' });
    await fixture.whenStable();
    const abertos = tela.querySelectorAll('.pv-opcional[open] summary');
    expect(abertos.length).toBe(1);
    expect(abertos[0].textContent).toContain('Caixa de e-mail própria');
  });
});

describe('EmpresasComponent: busca e ordenação', () => {
  it('a busca filtra pelo CNPJ com pontuação e o cabeçalho inverte a ordem', async () => {
    const { fixture, tela } = await montar();
    const componente = fixture.componentInstance;

    // Começa por nome, A a Z
    let nomes = Array.from(tela.querySelectorAll('tbody tr td:first-child strong')).map(e => e.textContent);
    expect(nomes).toEqual(['Loja Centro', 'Loja Norte']);

    const cabecalho = tela.querySelector<HTMLButtonElement>('th.pv-th-ordenavel .pv-coluna')!;
    cabecalho.click();
    await fixture.whenStable();
    nomes = Array.from(tela.querySelectorAll('tbody tr td:first-child strong')).map(e => e.textContent);
    expect(nomes).toEqual(['Loja Norte', 'Loja Centro']);
    expect(tela.querySelector('th.pv-th-ordenavel')!.getAttribute('aria-sort')).toBe('descending');

    componente.mudarBusca('11.222.333/0002');
    await fixture.whenStable();
    expect(tela.querySelectorAll('tbody tr').length).toBe(1);
    expect(tela.querySelector('tbody tr')!.textContent).toContain('Loja Norte');

    componente.mudarBusca('nenhuma');
    await fixture.whenStable();
    expect(tela.textContent).toContain('Nenhum resultado para "nenhuma"');
  });
});

