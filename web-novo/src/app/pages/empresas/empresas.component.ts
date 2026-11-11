import { Component, DestroyRef, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { aguardarFimDoJob } from '../../core/aguardar-job';
import { abrirJanelaBling, avisarRetornoBling, ehJanelaBling, ouvirRetornoBling } from '../../core/janela-bling';
import { formatarCnpj } from '../../core/implantacao';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { PREVISIO_CONFIG } from '../../core/previsio.config';
import { PrevisioService } from '../../core/previsio.service';
import { AlteracaoEmpresa, DESCRICAO_ORIGEM_APLICATIVO, Empresa, NovaEmpresa } from '../../core/previsio.models';
import { SessaoService } from '../../core/sessao.service';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { MenuAcoesComponent } from '../../shared/menu-acoes/menu-acoes.component';
import { TAMANHO_PAGINA_PADRAO, paginar } from '../../core/paginacao';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';
import { BuscaComponent } from '../../shared/busca/busca.component';
import { ColunaOrdenavelComponent } from '../../shared/coluna-ordenavel/coluna-ordenavel.component';
import { Ordenacao, alternarOrdenacao, combinaComBusca, ordenarLista } from '../../core/ordenacao';

type AcaoConfirmada = 'desativar' | 'desconectar';

/**
 * Empresas (CNPJs) do cliente exibido e a conexão de cada uma com o Bling.
 * O mesmo formulário do topo cadastra uma empresa nova ou, depois de "Editar", altera a escolhida.
 */
@Component({
  selector: 'app-empresas',
  imports: [FormsModule, RouterLink, DatePipe, MenuAcoesComponent, CarregandoDirective, PaginacaoComponent, BuscaComponent, ColunaOrdenavelComponent],
  templateUrl: './empresas.component.html'
})
export class EmpresasComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly rota = inject(ActivatedRoute);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  // Rodar os jobs na hora é só do admin (eles passam por todos os clientes)
  private readonly sessao = inject(SessaoService);
  readonly ehAdmin = this.sessao.ehAdmin;

  // Caixa da rede, mostrada na coluna E-mail das empresas sem caixa própria
  readonly emailRede = signal('');
  readonly empresas = signal<Empresa[]>([]);

  // Paginação da tabela (a API devolve a lista inteira)
  readonly pagina = signal(1);
  readonly tamanhoPagina = signal(TAMANHO_PAGINA_PADRAO);
  // Caixa de busca e ordenação por coluna, aplicadas aqui (a API devolve a lista inteira)
  readonly busca = signal('');
  readonly ordenacao = signal<Ordenacao | null>({ coluna: 'nome', decrescente: false });
  readonly empresasFiltradas = computed(() => {
    const termo = this.busca();
    return this.empresas().filter(item => combinaComBusca(termo, [item.nome, item.cnpj, item.imapUsuario]));
  });
  readonly empresasOrdenadas = computed(() => ordenarLista(this.empresasFiltradas(), this.ordenacao(), {
    nome: item => item.nome,
    cnpj: item => item.cnpj,
    email: item => item.emailProprio ? item.imapUsuario : this.emailRede(),
    bling: item => item.blingConectado ? 1 : 0,
    sincronizacao: item => item.ultimaSincronizacaoEm
  }));
  readonly empresasDaPagina = computed(() => paginar(this.empresasOrdenadas(), this.pagina(), this.tamanhoPagina()));
  // Só depois de a lista chegar dá para dizer "nenhuma empresa" (com erro, fica só o aviso)
  readonly carregada = signal(false);

  // Campos do formulário (cadastro e edição)
  readonly nome = signal('');
  readonly cnpj = signal('');
  readonly clientId = signal('');
  readonly clientSecret = signal('');

  // Caixa de e-mail própria (opcional): servidor vazio = notas só pela caixa da rede
  readonly imapHost = signal('');
  readonly imapPorta = signal<number | null>(993);
  readonly imapSsl = signal(true);
  readonly imapUsuario = signal('');
  readonly imapSenha = signal('');

  // Empresa sendo alterada no formulário; null = cadastro de uma nova
  readonly editando = signal<Empresa | null>(null);

  // O formulário fica fechado e abre em "+ Nova empresa" ou "Editar";
  // sem nenhuma empresa cadastrada ele já aparece aberto
  readonly formularioAberto = signal(false);
  readonly mostrarFormulario = computed(() => {
    return this.formularioAberto() || (this.carregada() && this.empresas().length === 0);
  });
  readonly linkCopiado = signal(false);

  // Endereço que deve ser cadastrado como "Link de redirecionamento" no aplicativo do Bling
  readonly linkRedirecionamento = PREVISIO_CONFIG.apiUrl + '/bling/callback';
  readonly mensagem = signal('');
  // Sincronização com o Bling rodando: o botão fica desabilitado até ela terminar
  readonly sincronizando = signal(false);
  readonly erro = signal('');

  // Desativar e desconectar pedem confirmação na própria linha
  readonly confirmacao = signal<{ empresaId: number; acao: AcaoConfirmada } | null>(null);
  readonly processandoId = signal<number | null>(null);
  // Cadastro de empresa nova em andamento
  readonly cadastrando = signal(false);

  /** Trocar o aplicativo de uma empresa conectada derruba a conexão (os tokens são do aplicativo anterior). */
  readonly vaiDesconectar = computed(() => {
    const empresa = this.editando();
    if (empresa === null) {
      return false;
    }
    return empresa.blingConectado && this.clientId().trim() !== (empresa.blingClientId ?? '');
  });

  private readonly cartaoFormulario = viewChild<ElementRef<HTMLElement>>('cartaoFormulario');
  private readonly campoNome = viewChild<ElementRef<HTMLInputElement>>('campoNome');

  constructor() {
    // Retorno do OAuth do Bling: /pages/empresas?bling=conectado
    const retornoBling = this.rota.snapshot.queryParamMap.get('bling');
    if (retornoBling !== null && ehJanelaBling()) {
      // Esta é a janela de autorização: avisa o Previsio que ficou aberto atrás e se fecha.
      // Se o navegador não deixar fechar, a própria janela mostra o resultado.
      avisarRetornoBling(retornoBling);
      window.close();
    } else {
      // Previsio aberto atrás da janela do Bling: atualiza quando a autorização terminar
      const pararDeOuvir = ouvirRetornoBling(retorno => {
        this.limparAvisos();
        this.mostrarRetornoBling(retorno);
        this.carregar();
        window.focus();
      });
      this.destroyRef.onDestroy(pararDeOuvir);
    }
    if (retornoBling !== null) {
      this.mostrarRetornoBling(retornoBling);
    }

    // Vindo da tela Clientes, logo depois de cadastrar a rede
    if (this.rota.snapshot.queryParamMap.get('cliente') === 'novo') {
      this.mensagem.set('Cliente cadastrado. Agora cadastre as empresas (CNPJs) dele no formulário abaixo e conecte o Bling de cada uma.');
      this.formularioAberto.set(true);
    }

    this.carregar();
    this.carregarEmailRede();
  }

  private carregarEmailRede(): void {
    const clienteId = this.sessao.clienteId();
    if (clienteId === null) {
      return;
    }
    this.previsio.obterCliente(clienteId).subscribe({
      next: cliente => this.emailRede.set(cliente.imapUsuario),
      // Sem o e-mail da rede a coluna mostra só "Caixa da rede"
      error: () => this.emailRede.set('')
    });
  }

  carregar(): void {
    this.previsio.listarEmpresas().subscribe({
      next: empresas => {
        this.empresas.set(empresas);
        this.carregada.set(true);
      },
      error: () => this.erro.set('Não foi possível carregar as empresas.')
    });
  }

  /** Envio do formulário: altera a empresa em edição ou cadastra uma nova. */
  salvar(): void {
    const empresa = this.editando();
    if (empresa !== null) {
      this.salvarEdicao(empresa);
    } else {
      this.adicionar();
    }
  }

  adicionar(): void {
    const nome = this.nome().trim();
    const cnpj = this.cnpj().replace(/\D/g, '');
    if (nome === '' || cnpj.length !== 14) {
      this.erro.set('Informe o nome e um CNPJ com 14 dígitos.');
      return;
    }

    const clientId = this.clientId().trim();
    const clientSecret = this.clientSecret().trim();
    if ((clientId === '') !== (clientSecret === '')) {
      this.erro.set('Informe o Client ID e o Client Secret do aplicativo do Bling, ou deixe os dois vazios.');
      return;
    }

    const dados: NovaEmpresa = { nome: nome, cnpj: cnpj };
    if (clientId !== '') {
      dados.blingClientId = clientId;
      dados.blingClientSecret = clientSecret;
    }

    if (!this.emailProprioValido(null)) {
      return;
    }
    if (this.imapHost().trim() !== '') {
      dados.imapHost = this.imapHost().trim();
      dados.imapPorta = Number(this.imapPorta());
      dados.imapSsl = this.imapSsl();
      dados.imapUsuario = this.imapUsuario().trim();
      dados.imapSenha = this.imapSenha();
    }

    this.limparAvisos();
    this.cadastrando.set(true);
    this.previsio.criarEmpresa(dados).subscribe({
      next: () => {
        this.cadastrando.set(false);
        this.limparFormulario();
        this.mensagem.set('Empresa cadastrada. Agora conecte a conta Bling dela.');
        this.carregar();
      },
      error: resposta => {
        this.cadastrando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível cadastrar a empresa.'));
      }
    });
  }

  /**
   * Abre o Bling numa janela separada para o cliente fazer login e autorizar o Previsio,
   * sem sair do Previsio. Com popup bloqueado, segue na mesma aba.
   */
  conectarBling(empresa: Empresa): void {
    this.limparAvisos();
    const janela = abrirJanelaBling();
    this.processandoId.set(empresa.id);
    this.previsio.obterUrlConexaoBling(empresa.id).subscribe({
      next: resposta => {
        this.processandoId.set(null);
        if (janela !== null && !janela.closed) {
          janela.location.href = resposta.url;
          janela.focus();
          this.mensagem.set('Conclua o login e a autorização na janela do Bling. Esta tela atualiza sozinha quando terminar.');
        } else {
          window.location.href = resposta.url;
        }
      },
      error: resposta => {
        this.processandoId.set(null);
        if (janela !== null) {
          janela.close();
        }
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível gerar o link de conexão.'));
      }
    });
  }

  private mostrarRetornoBling(retorno: string): void {
    if (retorno === 'conectado') {
      this.mensagem.set('Conta Bling conectada. As contas a pagar serão sincronizadas em instantes.');
    } else if (retorno === 'expirado') {
      this.erro.set('O link de conexão expirou. Clique em "Conectar Bling" novamente.');
    } else if (retorno === 'erro') {
      this.erro.set('Não foi possível conectar ao Bling. Tente novamente.');
    }
  }

  /** Busca as contas a pagar no Bling e atualiza a lista (última sincronização) sozinha quando termina. */
  sincronizarAgora(): void {
    this.limparAvisos();
    this.sincronizando.set(true);
    this.mensagem.set('Sincronizando as contas com o Bling... a lista atualiza sozinha quando terminar.');
    this.previsio.executarJob('sincronizacao').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.esperarSincronizacao(),
      error: () => {
        this.sincronizando.set(false);
        this.mensagem.set('');
        this.erro.set('Não foi possível iniciar a sincronização.');
      }
    });
  }

  private esperarSincronizacao(): void {
    aguardarFimDoJob(() => this.previsio.situacaoJob('sincronizacao'))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.sincronizando.set(false);
          this.mensagem.set('Sincronização concluída. As contas do Bling já aparecem no painel.');
          this.carregar();
        },
        error: () => {
          this.sincronizando.set(false);
          this.mensagem.set('A sincronização ainda não terminou. Confira o painel em alguns instantes.');
          this.carregar();
        }
      });
  }

  // ---------- formulário do topo: nova empresa ou edição ----------

  /** "+ Nova empresa": abre o formulário vazio e leva o foco ao nome. */
  abrirNovo(): void {
    this.confirmacao.set(null);
    this.limparAvisos();
    this.limparFormulario();
    this.formularioAberto.set(true);
    this.focarFormulario();
  }

  /** Copia o link de redirecionamento que vai no cadastro do aplicativo do Bling. */
  copiarLink(): void {
    if (!navigator.clipboard) {
      return;
    }
    navigator.clipboard.writeText(this.linkRedirecionamento).then(() => {
      this.linkCopiado.set(true);
      setTimeout(() => this.linkCopiado.set(false), 2000);
    });
  }

  /** CNPJ com pontuação: 11.222.333/0001-81. */
  formatarCnpj(cnpj: string): string {
    return formatarCnpj(cnpj);
  }

  private focarFormulario(): void {
    afterNextRender(() => {
      const cartao = this.cartaoFormulario();
      if (cartao) {
        cartao.nativeElement.scrollIntoView({ block: 'start' });
      }
      const campo = this.campoNome();
      if (campo) {
        campo.nativeElement.focus({ preventScroll: true });
      }
    }, { injector: this.injector });
  }

  /** Preenche o formulário com a empresa e leva a tela até ele. */
  editar(empresa: Empresa): void {
    this.confirmacao.set(null);
    this.limparAvisos();
    this.editando.set(empresa);
    this.nome.set(empresa.nome);
    this.cnpj.set(empresa.cnpj);
    this.clientId.set(empresa.blingClientId ?? '');
    this.clientSecret.set('');
    this.imapHost.set(empresa.imapHost);
    this.imapPorta.set(empresa.emailProprio ? empresa.imapPorta : 993);
    this.imapSsl.set(empresa.emailProprio ? empresa.imapSsl : true);
    this.imapUsuario.set(empresa.imapUsuario);
    this.imapSenha.set('');
    this.formularioAberto.set(true);
    this.focarFormulario();
  }

  descricaoOrigem(empresa: Empresa): string {
    return DESCRICAO_ORIGEM_APLICATIVO[empresa.origemAplicativoBling] ?? '';
  }

  /**
   * Com servidor preenchido, a caixa própria precisa de e-mail, porta e senha
   * (na edição, a senha já cadastrada vale enquanto a caixa for a mesma).
   */
  private emailProprioValido(empresa: Empresa | null): boolean {
    const host = this.imapHost().trim();
    if (host === '') {
      return true;
    }
    const usuario = this.imapUsuario().trim();
    const porta = Number(this.imapPorta());
    if (usuario === '') {
      this.erro.set('Informe o e-mail da caixa própria da empresa (ou deixe o servidor vazio).');
      return false;
    }
    if (!Number.isInteger(porta) || porta < 1 || porta > 65535) {
      this.erro.set('Informe uma porta entre 1 e 65535 (no Gmail, 993).');
      return false;
    }

    const mesmaCaixa = empresa !== null
      && empresa.imapSenhaCadastrada
      && host.toLowerCase() === empresa.imapHost.toLowerCase()
      && usuario.toLowerCase() === empresa.imapUsuario.toLowerCase();
    if (this.imapSenha().trim() === '' && !mesmaCaixa) {
      this.erro.set('Informe a senha de app do e-mail próprio da empresa.');
      return false;
    }
    return true;
  }

  estaEditando(empresa: Empresa): boolean {
    const editando = this.editando();
    return editando !== null && editando.id === empresa.id;
  }

  cancelarEdicao(): void {
    this.limparFormulario();
  }

  private salvarEdicao(empresa: Empresa): void {
    const nome = this.nome().trim();
    if (nome === '') {
      this.erro.set('Informe o nome da empresa.');
      return;
    }

    const clientId = this.clientId().trim();
    const clientSecret = this.clientSecret().trim();
    const trocouAplicativo = clientId !== (empresa.blingClientId ?? '');
    if (clientId !== '' && clientSecret === '' && (trocouAplicativo || !empresa.blingClientSecretCadastrado)) {
      this.erro.set('Informe o Client Secret do aplicativo do Bling.');
      return;
    }

    const dados: AlteracaoEmpresa = { nome: nome, blingClientId: clientId };
    if (clientSecret !== '') {
      dados.blingClientSecret = clientSecret;
    }

    if (!this.emailProprioValido(empresa)) {
      return;
    }
    // Só mexe no e-mail quando a empresa tem ou passa a ter caixa própria ("" remove a caixa)
    const host = this.imapHost().trim();
    if (host !== '' || empresa.emailProprio) {
      dados.imapHost = host;
    }
    if (host !== '') {
      dados.imapPorta = Number(this.imapPorta());
      dados.imapSsl = this.imapSsl();
      dados.imapUsuario = this.imapUsuario().trim();
      // Senha em branco mantém a atual
      if (this.imapSenha().trim() !== '') {
        dados.imapSenha = this.imapSenha();
      }
    }

    this.limparAvisos();
    this.processandoId.set(empresa.id);
    this.previsio.alterarEmpresa(empresa.id, dados).subscribe({
      next: alterada => {
        this.substituir(alterada);
        this.limparFormulario();
        this.processandoId.set(null);
        if (empresa.blingConectado && !alterada.blingConectado) {
          this.mensagem.set('Empresa alterada. O aplicativo do Bling mudou: clique em "Conectar Bling" para autorizar de novo.');
        } else {
          this.mensagem.set('Empresa alterada.');
        }
      },
      error: resposta => {
        this.processandoId.set(null);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível alterar a empresa.'));
      }
    });
  }

  // ---------- ativar, desativar e desconectar ----------

  pedirConfirmacao(empresa: Empresa, acao: AcaoConfirmada): void {
    this.confirmacao.set({ empresaId: empresa.id, acao: acao });
  }

  aguardandoConfirmacao(empresa: Empresa): boolean {
    const confirmacao = this.confirmacao();
    return confirmacao !== null && confirmacao.empresaId === empresa.id;
  }

  cancelarConfirmacao(): void {
    this.confirmacao.set(null);
  }

  confirmar(empresa: Empresa): void {
    const confirmacao = this.confirmacao();
    if (confirmacao === null || confirmacao.empresaId !== empresa.id) {
      return;
    }
    this.confirmacao.set(null);

    if (confirmacao.acao === 'desativar') {
      this.mudarSituacao(empresa, false);
    } else {
      this.desconectar(empresa);
    }
  }

  ativar(empresa: Empresa): void {
    this.mudarSituacao(empresa, true);
  }

  private mudarSituacao(empresa: Empresa, ativo: boolean): void {
    this.limparAvisos();
    this.processandoId.set(empresa.id);
    this.previsio.alterarEmpresa(empresa.id, { ativo: ativo }).subscribe({
      next: alterada => {
        this.substituir(alterada);
        this.processandoId.set(null);
        if (ativo) {
          this.mensagem.set('Empresa ativada.');
        } else {
          this.mensagem.set('Empresa desativada. Ela não aparece mais no painel.');
        }
      },
      error: resposta => {
        this.processandoId.set(null);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível alterar a empresa.'));
      }
    });
  }

  private desconectar(empresa: Empresa): void {
    this.limparAvisos();
    this.processandoId.set(empresa.id);
    this.previsio.desconectarBling(empresa.id).subscribe({
      next: () => {
        this.processandoId.set(null);
        this.mensagem.set('Empresa desconectada do Bling.');
        this.carregar();
      },
      error: resposta => {
        this.processandoId.set(null);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível desconectar a empresa do Bling.'));
      }
    });
  }

  private substituir(alterada: Empresa): void {
    this.empresas.set(this.empresas().map(empresa => {
      if (empresa.id === alterada.id) {
        return alterada;
      }
      return empresa;
    }));
    // A empresa em edição também passa a ser a versão nova (ex.: desativada pelo menu)
    const editando = this.editando();
    if (editando !== null && editando.id === alterada.id) {
      this.editando.set(alterada);
    }
  }

  // Também fecha o formulário: depois de salvar ou cancelar a tela volta para a lista
  private limparFormulario(): void {
    this.formularioAberto.set(false);
    this.editando.set(null);
    this.nome.set('');
    this.cnpj.set('');
    this.clientId.set('');
    this.clientSecret.set('');
    this.imapHost.set('');
    this.imapPorta.set(993);
    this.imapSsl.set(true);
    this.imapUsuario.set('');
    this.imapSenha.set('');
  }

  private limparAvisos(): void {
    this.erro.set('');
    this.mensagem.set('');
  }

  /** Clique no cabeçalho: ordena pela coluna e volta para a primeira página. */
  ordenarPor(coluna: string): void {
    this.ordenacao.set(alternarOrdenacao(this.ordenacao(), coluna));
    this.pagina.set(1);
  }

  /** Nova busca volta para a primeira página. */
  mudarBusca(texto: string): void {
    this.busca.set(texto);
    this.pagina.set(1);
  }
}
