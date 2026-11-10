import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';

import { PREVISIO_CONFIG } from './previsio.config';
import {
  AlteracaoCliente,
  AlteracaoEmpresa,
  AlteracaoUsuario,
  Cliente,
  Empresa,
  FiltroFalhas,
  Nota,
  NovaEmpresa,
  NovoCliente,
  NovoUsuario,
  PaginaFalhas,
  ResumoPainel,
  Usuario,
  VencimentoDia
} from './previsio.models';
import { SessaoService } from './sessao.service';
import { parametrosDasFalhas } from './filtro-falhas';

/**
 * Chamadas à API do Previsio.
 * O token JWT é colocado pelo autenticacaoInterceptor; o cliente vem do usuário logado.
 */
@Injectable({ providedIn: 'root' })
export class PrevisioService {
  private readonly http = inject(HttpClient);
  private readonly sessao = inject(SessaoService);
  private readonly api = PREVISIO_CONFIG.apiUrl;

  obterPainel(dias: number): Observable<ResumoPainel> {
    const params = new HttpParams().set('dias', String(dias));
    return this.doCliente(url => this.http.get<ResumoPainel>(`${url}/painel`, { params }));
  }

  obterVencimentos(empresaId: number, dias: number): Observable<VencimentoDia[]> {
    const params = new HttpParams().set('dias', String(dias));
    return this.http.get<VencimentoDia[]>(`${this.api}/empresas/${empresaId}/vencimentos`, { params });
  }

  obterNotasPendentes(empresaId?: number): Observable<Nota[]> {
    let params = new HttpParams();
    if (empresaId) {
      params = params.set('empresaId', String(empresaId));
    }
    return this.doCliente(url => this.http.get<Nota[]>(`${url}/notas/pendentes`, { params }));
  }

  /** Falhas ao ler notas do e-mail e ao lançá-las no Bling, da mais recente para a mais antiga. */
  obterFalhas(filtro: FiltroFalhas): Observable<PaginaFalhas> {
    const params = parametrosDasFalhas(filtro);
    return this.doCliente(url => this.http.get<PaginaFalhas>(`${url}/falhas`, { params }));
  }

  reprocessarNota(notaId: number): Observable<void> {
    return this.http.post<void>(`${this.api}/notas/${notaId}/reprocessar`, {});
  }

  listarEmpresas(): Observable<Empresa[]> {
    return this.doCliente(url => this.http.get<Empresa[]>(`${url}/empresas`));
  }

  criarEmpresa(dados: NovaEmpresa): Observable<Empresa> {
    return this.doCliente(url => this.http.post<Empresa>(`${url}/empresas`, dados));
  }

  /** Empresas de um cliente específico (tela de detalhe do cliente), não do cliente exibido. */
  listarEmpresasDoCliente(clienteId: number): Observable<Empresa[]> {
    return this.http.get<Empresa[]>(`${this.api}/clientes/${clienteId}/empresas`);
  }

  alterarEmpresa(empresaId: number, dados: AlteracaoEmpresa): Observable<Empresa> {
    return this.http.put<Empresa>(`${this.api}/empresas/${empresaId}`, dados);
  }

  obterUrlConexaoBling(empresaId: number): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.api}/empresas/${empresaId}/bling/conectar`);
  }

  desconectarBling(empresaId: number): Observable<void> {
    return this.http.delete<void>(`${this.api}/empresas/${empresaId}/bling`);
  }

  obterCliente(clienteId: number): Observable<Cliente> {
    return this.http.get<Cliente>(`${this.api}/clientes/${clienteId}`);
  }

  /** Só admin. */
  alterarCliente(clienteId: number, dados: AlteracaoCliente): Observable<Cliente> {
    return this.http.put<Cliente>(`${this.api}/clientes/${clienteId}`, dados);
  }

  /** Só admin. */
  listarClientes(): Observable<Cliente[]> {
    return this.http.get<Cliente[]>(`${this.api}/clientes`);
  }

  /** Só admin. */
  criarCliente(dados: NovoCliente): Observable<Cliente> {
    return this.http.post<Cliente>(`${this.api}/clientes`, dados);
  }

  /** Só admin. */
  listarUsuarios(): Observable<Usuario[]> {
    return this.http.get<Usuario[]>(`${this.api}/usuarios`);
  }

  /** Só admin. */
  criarUsuario(dados: NovoUsuario): Observable<Usuario> {
    return this.http.post<Usuario>(`${this.api}/usuarios`, dados);
  }

  /** Só admin. */
  alterarUsuario(usuarioId: number, dados: AlteracaoUsuario): Observable<Usuario> {
    return this.http.put<Usuario>(`${this.api}/usuarios/${usuarioId}`, dados);
  }

  /** Só admin. */
  executarJob(nome: 'leitura-email' | 'lancamento' | 'sincronizacao'): Observable<void> {
    return this.http.post<void>(`${this.api}/jobs/${nome}/executar`, {});
  }

  // Monta a URL /clientes/{id} do cliente atual. O layout só abre as telas
  // depois de haver um cliente, mas a checagem evita chamar /clientes/null.
  private doCliente<T>(chamada: (urlCliente: string) => Observable<T>): Observable<T> {
    const clienteId = this.sessao.clienteId();
    if (clienteId === null) {
      return throwError(() => new Error('Nenhum cliente selecionado.'));
    }
    return chamada(`${this.api}/clientes/${clienteId}`);
  }
}
