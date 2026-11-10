import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { PREVISIO_CONFIG } from './previsio.config';
import { Empresa, Nota, ResumoPainel, VencimentoDia } from './previsio.models';

@Injectable({ providedIn: 'root' })
export class PrevisioService {
  private readonly http = inject(HttpClient);
  private readonly api = PREVISIO_CONFIG.apiUrl;
  private readonly clienteId = PREVISIO_CONFIG.clienteId;

  private get headers(): HttpHeaders {
    return new HttpHeaders({ 'X-Api-Key': PREVISIO_CONFIG.apiKey });
  }

  obterPainel(dias: number): Observable<ResumoPainel> {
    const params = new HttpParams().set('dias', String(dias));
    return this.http.get<ResumoPainel>(`${this.api}/clientes/${this.clienteId}/painel`, {
      headers: this.headers,
      params
    });
  }

  obterVencimentos(empresaId: number, dias: number): Observable<VencimentoDia[]> {
    const params = new HttpParams().set('dias', String(dias));
    return this.http.get<VencimentoDia[]>(`${this.api}/empresas/${empresaId}/vencimentos`, {
      headers: this.headers,
      params
    });
  }

  obterNotasPendentes(empresaId?: number): Observable<Nota[]> {
    let params = new HttpParams();
    if (empresaId) {
      params = params.set('empresaId', String(empresaId));
    }
    return this.http.get<Nota[]>(`${this.api}/clientes/${this.clienteId}/notas/pendentes`, {
      headers: this.headers,
      params
    });
  }

  reprocessarNota(notaId: number): Observable<void> {
    return this.http.post<void>(`${this.api}/notas/${notaId}/reprocessar`, {}, { headers: this.headers });
  }

  listarEmpresas(): Observable<Empresa[]> {
    return this.http.get<Empresa[]>(`${this.api}/clientes/${this.clienteId}/empresas`, { headers: this.headers });
  }

  criarEmpresa(nome: string, cnpj: string): Observable<Empresa> {
    return this.http.post<Empresa>(
      `${this.api}/clientes/${this.clienteId}/empresas`,
      { nome, cnpj },
      { headers: this.headers }
    );
  }

  obterUrlConexaoBling(empresaId: number): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.api}/empresas/${empresaId}/bling/conectar`, {
      headers: this.headers
    });
  }

  executarJob(nome: 'leitura-email' | 'lancamento' | 'sincronizacao'): Observable<void> {
    return this.http.post<void>(`${this.api}/jobs/${nome}/executar`, {}, { headers: this.headers });
  }
}
