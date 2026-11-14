import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpParams, HttpResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';

import { SEM_BARRA_CARREGAMENTO } from './carregamento.interceptor';
import { PREVISIO_CONFIG } from './previsio.config';
import {
  AlteracaoCliente,
  AlteracaoEmpresa,
  AlteracaoResumoSemanal,
  AlteracaoUsuario,
  AlertasVencimento,
  ConferenciaBling,
  Cliente,
  EmailIgnorado,
  Empresa,
  FiltroContas,
  FiltroFalhas,
  FiltroNotas,
  NomeJob,
  Nota,
  NovaEmpresa,
  NovoCliente,
  NovoUsuario,
  NotaSoPdf,
  PaginaContasPagar,
  PaginaContasReceber,
  FluxoCaixa,
  ConfiguracaoCategorias,
  NovaRegraCategoria,
  SituacaoSincronizacao,
  PaginaFalhas,
  PaginaNotas,
  PainelAvisos,
  SituacaoJob,
  ResumoPainel,
  ResumoSemanal,
  Saude,
  ResultadoTesteEmail,
  TesteEmail,
  TratamentoEmailIgnorado,
  Usuario,
  VencimentoDia
} from './previsio.models';
import { SessaoService } from './sessao.service';
import { parametrosDasContas } from './filtro-contas';
import { parametrosDasFalhas } from './filtro-falhas';
import { parametrosDasNotas } from './filtro-notas';

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

  /** Quantas notas estão pendentes (o número do menu). Chamado de tempos em tempos, sem a barra de carregamento. */
  obterTotalPendentes(): Observable<number> {
    const context = new HttpContext().set(SEM_BARRA_CARREGAMENTO, true);
    return this.doCliente(url => this.http.get<number>(`${url}/notas/pendentes/total`, { context }));
  }

  /** Todas as notas lidas do e-mail (lançadas ou não), da recebida mais recente para a mais antiga. */
  obterNotas(filtro: FiltroNotas): Observable<PaginaNotas> {
    const params = parametrosDasNotas(filtro);
    return this.doCliente(url => this.http.get<PaginaNotas>(`${url}/notas`, { params }));
  }

  /** Planilha das notas com os mesmos filtros, busca e ordem da lista (uma linha por parcela). */
  exportarNotas(filtro: FiltroNotas): Observable<HttpResponse<Blob>> {
    const params = parametrosDasNotas({ ...filtro, pagina: 1 });
    return this.doCliente(url => this.http.get(`${url}/notas/exportar`, { params, observe: 'response', responseType: 'blob' }));
  }

  /** Contas a pagar sincronizadas do Bling (lançadas pelo Previsio ou à mão), com os filtros da tela. */
  obterContasPagar(filtro: FiltroContas): Observable<PaginaContasPagar> {
    const params = parametrosDasContas(filtro);
    return this.doCliente(url => this.http.get<PaginaContasPagar>(`${url}/contas-pagar`, { params }));
  }

  /** Planilha das contas a pagar com os mesmos filtros e ordem da tela Contas a pagar. */
  exportarContasFiltradas(filtro: FiltroContas): Observable<HttpResponse<Blob>> {
    const params = parametrosDasContas({ ...filtro, pagina: 1 });
    return this.doCliente(url => this.http.get(`${url}/contas-pagar/exportar`, { params, observe: 'response', responseType: 'blob' }));
  }

  /** Pede a sincronização das contas só da rede atual (409 se sincronizou há menos de 2 minutos). */
  sincronizarRede(): Observable<SituacaoSincronizacao> {
    return this.doCliente(url => this.http.post<SituacaoSincronizacao>(`${url}/sincronizacao`, {}));
  }

  /** Se a sincronização da rede ainda está rodando. Consultado de tempos em tempos, sem a barra de carregamento. */
  situacaoSincronizacao(): Observable<SituacaoSincronizacao> {
    const context = new HttpContext().set(SEM_BARRA_CARREGAMENTO, true);
    return this.doCliente(url => this.http.get<SituacaoSincronizacao>(`${url}/sincronizacao`, { context }));
  }

  /** Contas a receber sincronizadas do Bling (fluxo de caixa), com os filtros da tela. */
  obterContasReceber(filtro: FiltroContas): Observable<PaginaContasReceber> {
    const params = parametrosDasContas(filtro);
    return this.doCliente(url => this.http.get<PaginaContasReceber>(`${url}/contas-receber`, { params }));
  }

  /** Planilha das contas a receber com os mesmos filtros e ordem da tela. */
  exportarContasReceber(filtro: FiltroContas): Observable<HttpResponse<Blob>> {
    const params = parametrosDasContas({ ...filtro, pagina: 1 });
    return this.doCliente(url => this.http.get(`${url}/contas-receber/exportar`, { params, observe: 'response', responseType: 'blob' }));
  }

  obterFluxoCaixa(clienteId: number): Observable<FluxoCaixa> {
    return this.http.get<FluxoCaixa>(`${this.api}/clientes/${clienteId}/fluxo-caixa`);
  }

  alterarFluxoCaixa(clienteId: number, dados: FluxoCaixa): Observable<FluxoCaixa> {
    return this.http.put<FluxoCaixa>(`${this.api}/clientes/${clienteId}/fluxo-caixa`, dados);
  }

  obterCategorias(clienteId: number): Observable<ConfiguracaoCategorias> {
    return this.http.get<ConfiguracaoCategorias>(`${this.api}/clientes/${clienteId}/categorias`);
  }

  /** Categoria padrão da rede pelo nome (vazio tira). */
  alterarCategoriaPadrao(clienteId: number, categoria: string): Observable<ConfiguracaoCategorias> {
    return this.http.put<ConfiguracaoCategorias>(`${this.api}/clientes/${clienteId}/categorias/padrao`, { categoria });
  }

  /** Cria a regra do fornecedor ou troca a categoria da que já existe. */
  salvarRegraCategoria(clienteId: number, regra: NovaRegraCategoria): Observable<ConfiguracaoCategorias> {
    return this.http.post<ConfiguracaoCategorias>(`${this.api}/clientes/${clienteId}/categorias/regras`, regra);
  }

  removerRegraCategoria(clienteId: number, regraId: number): Observable<ConfiguracaoCategorias> {
    return this.http.delete<ConfiguracaoCategorias>(`${this.api}/clientes/${clienteId}/categorias/regras/${regraId}`);
  }

  /** Planilha das contas a pagar em aberto no Bling dos próximos N dias (as do painel). */
  exportarContasPagar(dias: number, empresaId?: number): Observable<HttpResponse<Blob>> {
    let params = new HttpParams().set('dias', String(dias));
    if (empresaId) {
      params = params.set('empresaId', String(empresaId));
    }
    return this.doCliente(url => this.http.get(`${url}/contas-pagar/exportar`, { params, observe: 'response', responseType: 'blob' }));
  }

  /** Falhas ao ler notas do e-mail e ao lançá-las no Bling, da mais recente para a mais antiga. */
  obterFalhas(filtro: FiltroFalhas): Observable<PaginaFalhas> {
    const params = parametrosDasFalhas(filtro);
    return this.doCliente(url => this.http.get<PaginaFalhas>(`${url}/falhas`, { params }));
  }

  /** Avisos em aberto do usuário logado. Chamado de tempos em tempos, sem a barra de carregamento. */
  obterAvisos(): Observable<PainelAvisos> {
    const context = new HttpContext().set(SEM_BARRA_CARREGAMENTO, true);
    return this.http.get<PainelAvisos>(`${this.api}/avisos`, { context });
  }

  /** Marca como vistos os avisos abertos até agora (zera o número do sininho). */
  marcarAvisosVistos(): Observable<void> {
    const context = new HttpContext().set(SEM_BARRA_CARREGAMENTO, true);
    return this.http.post<void>(`${this.api}/avisos/vistos`, {}, { context });
  }

  /** Saúde das integrações de todas as redes (admin). */
  obterSaude(): Observable<Saude> {
    return this.http.get<Saude>(`${this.api}/saude`);
  }

  /** E-mails com anexo que não viraram nota nos últimos N dias (admin). */
  obterEmailsIgnorados(clienteId: number | null, dias: number): Observable<EmailIgnorado[]> {
    let params = new HttpParams().set('dias', String(dias));
    if (clienteId) {
      params = params.set('clienteId', String(clienteId));
    }
    return this.http.get<EmailIgnorado[]>(`${this.api}/saude/emails-ignorados`, { params });
  }

  baixarPdfEmailIgnorado(id: number): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.api}/saude/emails-ignorados/${id}/pdf`, { observe: 'response', responseType: 'blob' });
  }

  tratarEmailIgnoradoComoNota(id: number): Observable<TratamentoEmailIgnorado> {
    return this.http.post<TratamentoEmailIgnorado>(`${this.api}/saude/emails-ignorados/${id}/tratar-como-nota`, {});
  }

  obterConferenciaBling(clienteId: number): Observable<ConferenciaBling> {
    return this.http.get<ConferenciaBling>(`${this.api}/clientes/${clienteId}/conferencia-bling`);
  }

  alterarConferenciaBling(clienteId: number, dados: ConferenciaBling): Observable<ConferenciaBling> {
    return this.http.put<ConferenciaBling>(`${this.api}/clientes/${clienteId}/conferencia-bling`, dados);
  }

  obterAlertasVencimento(clienteId: number): Observable<AlertasVencimento> {
    return this.http.get<AlertasVencimento>(`${this.api}/clientes/${clienteId}/alertas-vencimento`);
  }

  alterarAlertasVencimento(clienteId: number, dados: AlertasVencimento): Observable<AlertasVencimento> {
    return this.http.put<AlertasVencimento>(`${this.api}/clientes/${clienteId}/alertas-vencimento`, dados);
  }

  obterResumoSemanal(clienteId: number): Observable<ResumoSemanal> {
    return this.http.get<ResumoSemanal>(`${this.api}/clientes/${clienteId}/resumo-semanal`);
  }

  alterarResumoSemanal(clienteId: number, dados: AlteracaoResumoSemanal): Observable<ResumoSemanal> {
    return this.http.put<ResumoSemanal>(`${this.api}/clientes/${clienteId}/resumo-semanal`, dados);
  }

  /** Manda o resumo agora, só para o usuário logado. Não conta como o envio da semana. */
  enviarResumoSemanalTeste(clienteId: number): Observable<void> {
    return this.http.post<void>(`${this.api}/clientes/${clienteId}/resumo-semanal/teste`, {});
  }

  /** Notas que chegaram por e-mail só em PDF e ainda precisam do XML. */
  obterNotasSoPdf(empresaId?: number): Observable<NotaSoPdf[]> {
    let params = new HttpParams();
    if (empresaId) {
      params = params.set('empresaId', String(empresaId));
    }
    return this.doCliente(url => this.http.get<NotaSoPdf[]>(`${url}/notas-so-pdf`, { params }));
  }

  baixarPdfNotaSoPdf(id: number): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.api}/notas-so-pdf/${id}/pdf`, { observe: 'response', responseType: 'blob' });
  }

  anexarXmlNotaSoPdf(id: number, xml: string): Observable<void> {
    return this.http.post<void>(`${this.api}/notas-so-pdf/${id}/xml`, { xml: xml });
  }

  informarChaveNotaSoPdf(id: number, chave: string): Observable<void> {
    return this.http.post<void>(`${this.api}/notas-so-pdf/${id}/chave`, { chave: chave });
  }

  pedirXmlNotaSoPdf(id: number): Observable<void> {
    return this.http.post<void>(`${this.api}/notas-so-pdf/${id}/pedir-xml`, {});
  }

  marcarNotaSoPdfLancada(id: number, observacao: string): Observable<void> {
    return this.http.post<void>(`${this.api}/notas-so-pdf/${id}/lancada-manualmente`, { observacao: observacao });
  }

  /** Envia o certificado A1 (.pfx) da empresa, com a senha e a UF. */
  salvarCertificado(empresaId: number, arquivo: File, senha: string, uf: string): Observable<Empresa> {
    const dados = new FormData();
    dados.append('arquivo', arquivo, arquivo.name);
    dados.append('senha', senha);
    dados.append('uf', uf);
    return this.http.post<Empresa>(`${this.api}/empresas/${empresaId}/certificado`, dados);
  }

  /** Liga ou desliga a busca completa das NF-e da empresa na SEFAZ. */
  alterarBuscaSefaz(empresaId: number, ativa: boolean): Observable<Empresa> {
    return this.http.put<Empresa>(`${this.api}/empresas/${empresaId}/sefaz`, { ativa });
  }

  removerCertificado(empresaId: number): Observable<Empresa> {
    return this.http.delete<Empresa>(`${this.api}/empresas/${empresaId}/certificado`);
  }

  /** Marca a nota com problema como resolvida e lançada manualmente no Bling. */
  marcarLancadaManualmente(notaId: number, observacao: string): Observable<void> {
    return this.http.post<void>(`${this.api}/notas/${notaId}/lancada-manualmente`, { observacao: observacao });
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

  /** Cadastra a empresa num cliente específico (implantação), não no cliente exibido. */
  criarEmpresaDoCliente(clienteId: number, dados: NovaEmpresa): Observable<Empresa> {
    return this.http.post<Empresa>(`${this.api}/clientes/${clienteId}/empresas`, dados);
  }

  /** "Esqueci minha senha": a API responde igual exista ou não o e-mail. */
  solicitarRedefinicaoSenha(email: string): Observable<string> {
    return this.http.post(`${this.api}/auth/esqueci-senha`, { email: email }, { responseType: 'text' });
  }

  /** Nova senha pelo token do link enviado por e-mail. */
  redefinirSenha(token: string, novaSenha: string): Observable<void> {
    return this.http.post<void>(`${this.api}/auth/redefinir-senha`, { token: token, novaSenha: novaSenha });
  }

  /** Só admin. Conecta e entra na caixa, sem ler nem gravar nada. */
  testarEmail(dados: TesteEmail): Observable<ResultadoTesteEmail> {
    return this.http.post<ResultadoTesteEmail>(`${this.api}/email/testar`, dados);
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

  /** A rede contratou: sai do teste grátis (admin). */
  contratarCliente(clienteId: number): Observable<Cliente> {
    return this.http.post<Cliente>(`${this.api}/clientes/${clienteId}/contratar`, {});
  }

  /** Mais dias de teste grátis, do fim atual ou de hoje se já acabou (admin). */
  prorrogarTeste(clienteId: number, dias: number): Observable<Cliente> {
    return this.http.post<Cliente>(`${this.api}/clientes/${clienteId}/prorrogar-teste`, { dias: dias });
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
  executarJob(nome: NomeJob): Observable<void> {
    return this.http.post<void>(`${this.api}/jobs/${nome}/executar`, {});
  }

  /** Só admin. Se o job ainda está na fila ou rodando. */
  situacaoJob(nome: NomeJob): Observable<SituacaoJob> {
    return this.http.get<SituacaoJob>(`${this.api}/jobs/${nome}/situacao`);
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
