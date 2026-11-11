import { Component, DestroyRef, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { aguardarFimDoJob } from '../../core/aguardar-job';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { PREVISIO_CONFIG } from '../../core/previsio.config';
import { PrevisioService } from '../../core/previsio.service';
import { AlteracaoEmpresa, Empresa, NovaEmpresa } from '../../core/previsio.models';
import { SessaoService } from '../../core/sessao.service';
import { MenuAcoesComponent } from '../../shared/menu-acoes/menu-acoes.component';

type AcaoConfirmada = 'desativar' | 'desconectar';

/**
 * Empresas (CNPJs) do cliente exibido e a conexão de cada uma com o Bling.
 * O mesmo formulário do topo cadastra uma empresa nova ou, depois de "Editar", altera a escolhida.
 */
@Component({
  selector: 'app-empresas',
  imports: [FormsModule, RouterLink, DatePipe, MenuAcoesComponent],
  templateUrl: './empresas.component.html'
})
export class EmpresasComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly rota = inject(ActivatedRoute);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  // Rodar os jobs na hora é só do admin (eles passam por todos os clientes)
  readonly ehAdmin = inject(SessaoService).ehAdmin;
  readonly empresas = signal<Empresa[]>([]);
  // Só depois de a lista chegar dá para dizer "nenhuma empresa" (com erro, fica só o aviso)
  readonly carregada = signal(false);

  // Campos do formulário (cadastro e edição)
  readonly nome = signal('');
  readonly cnpj = signal('');
  readonly clientId = signal('');
  readonly clientSecret = signal('');

  // Empresa sendo alterada no formulário; null = cadastro de uma nova
  readonly editando = signal<Empresa | null>(null);

  // Endereço que deve ser cadastrado como "Link de redirecionamento" no aplicativo do Bling
  readonly linkRedirecionamento = PREVISIO_CONFIG.apiUrl + '/bling/callback';
  readonly mensagem = signal('');
  // Sincronização com o Bling rodando: o botão fica desabilitado até ela terminar
  readonly sincronizando = signal(false);
  readonly erro = signal('');

  // Desativar e desconectar pedem confirmação na própria linha
  readonly confirmacao = signal<{ empresaId: number; acao: AcaoConfirmada } | null>(null);
  readonly processandoId = signal<number | null>(null);

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
    if (retornoBling === 'conectado') {
      this.mensagem.set('Conta Bling conectada. As contas a pagar serão sincronizadas em instantes.');
    } else if (retornoBling === 'expirado') {
      this.erro.set('O link de conexão expirou. Clique em "Conectar Bling" novamente.');
    } else if (retornoBling === 'erro') {
      this.erro.set('Não foi possível conectar ao Bling. Tente novamente.');
    }

    // Vindo da tela Clientes, logo depois de cadastrar a rede
    if (this.rota.snapshot.queryParamMap.get('cliente') === 'novo') {
      this.mensagem.set('Cliente cadastrado. Agora cadastre as empresas (CNPJs) dele no formulário abaixo e conecte o Bling de cada uma.');
    }

    this.carregar();
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

    this.limparAvisos();
    this.previsio.criarEmpresa(dados).subscribe({
      next: () => {
        this.limparFormulario();
        this.mensagem.set('Empresa cadastrada. Agora conecte a conta Bling dela.');
        this.carregar();
      },
      error: resposta => this.erro.set(mensagemDaApi(resposta, 'Não foi possível cadastrar a empresa.'))
    });
  }

  conectarBling(empresa: Empresa): void {
    this.previsio.obterUrlConexaoBling(empresa.id).subscribe({
      next: resposta => {
        // O cliente faz login no Bling da empresa e autoriza o Previsio
        window.location.href = resposta.url;
      },
      error: resposta => this.erro.set(mensagemDaApi(resposta, 'Não foi possível gerar o link de conexão.'))
    });
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

  // ---------- edição no formulário do topo ----------

  /** Preenche o formulário com a empresa e leva a tela até ele. */
  editar(empresa: Empresa): void {
    this.confirmacao.set(null);
    this.limparAvisos();
    this.editando.set(empresa);
    this.nome.set(empresa.nome);
    this.cnpj.set(empresa.cnpj);
    this.clientId.set(empresa.blingClientId ?? '');
    this.clientSecret.set('');

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

  private limparFormulario(): void {
    this.editando.set(null);
    this.nome.set('');
    this.cnpj.set('');
    this.clientId.set('');
    this.clientSecret.set('');
  }

  private limparAvisos(): void {
    this.erro.set('');
    this.mensagem.set('');
  }
}
