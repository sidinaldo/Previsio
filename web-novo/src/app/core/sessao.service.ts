import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { PREVISIO_CONFIG } from './previsio.config';
import { PERFIL_ADMIN, RespostaLogin, Usuario } from './previsio.models';

const CHAVE_SESSAO = 'previsio.sessao';
const CHAVE_CLIENTE = 'previsio.cliente';

/**
 * Login no Previsio.
 * Guarda o token JWT devolvido pela API e os dados do usuário no localStorage,
 * para o login sobreviver à ida e volta do OAuth do Bling e a novas abas.
 * A API continua sendo quem decide: token vencido ou revogado volta 401 e o painel sai.
 */
@Injectable({ providedIn: 'root' })
export class SessaoService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly sessao = signal<RespostaLogin | null>(lerSessaoSalva());
  private readonly clienteEscolhido = signal<number | null>(lerClienteEscolhido());

  readonly usuario = computed<Usuario | null>(() => {
    const sessao = this.sessao();
    if (sessao) {
      return sessao.usuario;
    }
    return null;
  });

  readonly ehAdmin = computed(() => {
    const usuario = this.usuario();
    return usuario !== null && usuario.perfil === PERFIL_ADMIN;
  });

  /**
   * Cliente cujos dados o painel mostra: o do próprio usuário ou,
   * para o admin, o escolhido na barra do topo.
   */
  readonly clienteId = computed<number | null>(() => {
    const usuario = this.usuario();
    if (!usuario) {
      return null;
    }
    if (usuario.perfil === PERFIL_ADMIN) {
      return this.clienteEscolhido();
    }
    return usuario.clienteId;
  });

  entrar(email: string, senha: string): Observable<RespostaLogin> {
    return this.http
      .post<RespostaLogin>(`${PREVISIO_CONFIG.apiUrl}/auth/login`, { email, senha })
      .pipe(tap(resposta => this.guardar(resposta)));
  }

  /** Token para enviar à API, ou null se não há login ou se ele já venceu. */
  token(): string | null {
    const sessao = this.sessao();
    if (!sessao) {
      return null;
    }
    if (venceu(sessao)) {
      this.limpar();
      return null;
    }
    return sessao.token;
  }

  estaLogado(): boolean {
    return this.token() !== null;
  }

  escolherCliente(clienteId: number): void {
    this.clienteEscolhido.set(clienteId);
    gravar(CHAVE_CLIENTE, String(clienteId));
  }

  /**
   * Encerra o login e volta para a tela de entrada.
   * Com "expirou", a tela explica o motivo e, depois de entrar, volta para onde o usuário estava.
   */
  sair(motivo?: 'expirou'): void {
    const urlAtual = this.router.url;
    this.limpar();

    if (motivo === 'expirou') {
      const parametros: { [nome: string]: string } = { motivo: motivo };
      if (urlAtual.startsWith('/pages/') && !urlAtual.startsWith('/pages/login')) {
        parametros['retorno'] = urlAtual;
      }
      this.router.navigate(['/pages/login'], { queryParams: parametros });
    } else {
      this.router.navigate(['/pages/login']);
    }
  }

  private guardar(resposta: RespostaLogin): void {
    this.sessao.set(resposta);
    gravar(CHAVE_SESSAO, JSON.stringify(resposta));
  }

  private limpar(): void {
    this.sessao.set(null);
    remover(CHAVE_SESSAO);
  }
}

function lerSessaoSalva(): RespostaLogin | null {
  const texto = ler(CHAVE_SESSAO);
  if (!texto) {
    return null;
  }

  let sessao: RespostaLogin;
  try {
    sessao = JSON.parse(texto) as RespostaLogin;
  } catch {
    remover(CHAVE_SESSAO);
    return null;
  }

  if (!sessao || typeof sessao.token !== 'string' || typeof sessao.expiraEm !== 'string' || !sessao.usuario) {
    remover(CHAVE_SESSAO);
    return null;
  }
  if (venceu(sessao)) {
    remover(CHAVE_SESSAO);
    return null;
  }
  return sessao;
}

function lerClienteEscolhido(): number | null {
  const texto = ler(CHAVE_CLIENTE);
  if (!texto) {
    return null;
  }
  const id = Number(texto);
  if (!Number.isInteger(id) || id <= 0) {
    return null;
  }
  return id;
}

function venceu(sessao: RespostaLogin): boolean {
  const expiraEm = Date.parse(sessao.expiraEm);
  // Data ilegível conta como vencida
  if (isNaN(expiraEm)) {
    return true;
  }
  return expiraEm <= Date.now();
}

// O navegador pode bloquear o localStorage (modo privado, política da empresa):
// nesse caso o login vale só até a página ser recarregada.
function ler(chave: string): string | null {
  try {
    return localStorage.getItem(chave);
  } catch {
    return null;
  }
}

function gravar(chave: string, valor: string): void {
  try {
    localStorage.setItem(chave, valor);
  } catch {
    // Sem armazenamento: segue só com a memória
  }
}

function remover(chave: string): void {
  try {
    localStorage.removeItem(chave);
  } catch {
    // Sem armazenamento: nada a remover
  }
}
