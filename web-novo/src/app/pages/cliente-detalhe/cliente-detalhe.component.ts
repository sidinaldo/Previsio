import { Component, ElementRef, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { avisarCamposPreenchidos } from '../../core/campos-preenchidos';
import { ClientesService } from '../../core/clientes.service';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { PREVISIO_CONFIG } from '../../core/previsio.config';
import { AlteracaoCliente, Cliente, DESCRICAO_ORIGEM_APLICATIVO, Empresa } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { TAMANHO_PAGINA_PADRAO, paginar } from '../../core/paginacao';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';

/**
 * Edição de um cliente (só admin): caixa de e-mail e aplicativo do Bling da rede,
 * ativar/desativar e as empresas dele.
 */
@Component({
  selector: 'app-cliente-detalhe',
  imports: [FormsModule, RouterLink, DatePipe, CarregandoDirective, PaginacaoComponent],
  templateUrl: './cliente-detalhe.component.html'
})
export class ClienteDetalheComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly clientes = inject(ClientesService);
  private readonly sessao = inject(SessaoService);
  private readonly router = inject(Router);
  private readonly elemento = inject(ElementRef<HTMLElement>);

  private readonly clienteId = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  readonly cliente = signal<Cliente | null>(null);
  readonly empresas = signal<Empresa[]>([]);

  // Paginação da tabela (a API devolve a lista inteira)
  readonly pagina = signal(1);
  readonly tamanhoPagina = signal(TAMANHO_PAGINA_PADRAO);
  readonly empresasDaPagina = computed(() => paginar(this.empresas(), this.pagina(), this.tamanhoPagina()));
  readonly carregando = signal(true);

  readonly nome = signal('');
  readonly imapHost = signal('');
  readonly imapPorta = signal<number | null>(993);
  readonly imapSsl = signal(true);
  readonly imapUsuario = signal('');
  readonly imapSenha = signal('');

  // Aplicativo do Bling da rede: vale para as empresas que não têm um próprio
  readonly clientId = signal('');
  readonly clientSecret = signal('');

  // Endereço que deve ser cadastrado como "Link de redirecionamento" no aplicativo do Bling
  readonly linkRedirecionamento = PREVISIO_CONFIG.apiUrl + '/bling/callback';

  /** Trocar o aplicativo da rede desconecta as empresas que usam ele (os tokens são do aplicativo anterior). */
  readonly vaiDesconectarEmpresas = computed(() => {
    const cliente = this.cliente();
    if (cliente === null) {
      return false;
    }
    const usamARede = this.empresas().some(e => e.origemAplicativoBling === 'Rede' && e.blingConectado);
    return usamARede && this.clientId().trim() !== (cliente.blingClientId ?? '');
  });

  readonly salvando = signal(false);
  // Qual botão disparou o salvamento, para o spinner aparecer só nele
  readonly acao = signal<'salvar' | 'situacao' | null>(null);
  readonly mensagem = signal('');
  readonly erro = signal('');

  constructor() {
    this.previsio.obterCliente(this.clienteId).subscribe({
      next: cliente => {
        this.preencher(cliente);
        this.carregando.set(false);
      },
      error: resposta => {
        this.carregando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível carregar o cliente.'));
      }
    });

    this.carregarEmpresas();
  }

  private carregarEmpresas(): void {
    this.previsio.listarEmpresasDoCliente(this.clienteId).subscribe({
      next: empresas => this.empresas.set(empresas),
      error: () => this.erro.set('Não foi possível carregar as empresas do cliente.')
    });
  }

  descricaoOrigem(empresa: Empresa): string {
    return DESCRICAO_ORIGEM_APLICATIVO[empresa.origemAplicativoBling] ?? '';
  }

  salvar(): void {
    const nome = this.nome().trim();
    const host = this.imapHost().trim();
    const usuario = this.imapUsuario().trim();
    const porta = Number(this.imapPorta());

    if (nome === '') {
      this.erro.set('Informe o nome do cliente.');
      return;
    }
    if (host === '' || usuario === '') {
      this.erro.set('Informe o servidor e o e-mail da caixa dedicada.');
      return;
    }
    if (!Number.isInteger(porta) || porta < 1 || porta > 65535) {
      this.erro.set('Informe uma porta entre 1 e 65535 (no Gmail, 993).');
      return;
    }

    const cliente = this.cliente();
    const clientId = this.clientId().trim();
    const clientSecret = this.clientSecret().trim();
    const trocouAplicativo = clientId !== (cliente?.blingClientId ?? '');
    const temSecret = cliente !== null && cliente.blingClientSecretCadastrado;
    if (clientId !== '' && clientSecret === '' && (trocouAplicativo || !temSecret)) {
      this.erro.set('Informe o Client Secret do aplicativo do Bling da rede.');
      return;
    }

    const dados: AlteracaoCliente = {
      nome: nome,
      imapHost: host,
      imapPorta: porta,
      imapSsl: this.imapSsl(),
      imapUsuario: usuario,
      blingClientId: clientId
    };
    // Senha e secret em branco mantêm os atuais
    if (this.imapSenha().trim() !== '') {
      dados.imapSenha = this.imapSenha();
    }
    if (clientSecret !== '') {
      dados.blingClientSecret = clientSecret;
    }

    const desconectou = this.vaiDesconectarEmpresas();
    this.acao.set('salvar');
    this.enviar(dados, 'Cliente alterado.', desconectou);
  }

  alternarAtivo(): void {
    const cliente = this.cliente();
    if (cliente === null) {
      return;
    }
    this.acao.set('situacao');
    if (cliente.ativo) {
      this.enviar({ ativo: false }, 'Cliente desativado. Os usuários dele não conseguem mais entrar.');
    } else {
      this.enviar({ ativo: true }, 'Cliente ativado.');
    }
  }

  verEmpresas(): void {
    this.sessao.escolherCliente(this.clienteId);
    this.router.navigate(['/pages/empresas']);
  }

  private enviar(dados: AlteracaoCliente, sucesso: string, recarregarEmpresas = false): void {
    this.erro.set('');
    this.mensagem.set('');
    this.salvando.set(true);
    this.previsio.alterarCliente(this.clienteId, dados).subscribe({
      next: cliente => {
        this.salvando.set(false);
        this.preencher(cliente);
        this.clientes.atualizar(cliente);
        this.mensagem.set(sucesso);
        if (recarregarEmpresas) {
          this.mensagem.set('Cliente alterado. O aplicativo do Bling da rede mudou: conecte de novo o Bling das empresas que usam ele.');
        }
        // A origem do aplicativo de cada empresa pode ter mudado
        this.carregarEmpresas();
      },
      error: resposta => {
        this.salvando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível alterar o cliente.'));
      }
    });
  }

  private preencher(cliente: Cliente): void {
    this.cliente.set(cliente);
    this.nome.set(cliente.nome);
    this.imapHost.set(cliente.imapHost);
    this.imapPorta.set(cliente.imapPorta);
    this.imapSsl.set(cliente.imapSsl);
    this.imapUsuario.set(cliente.imapUsuario);
    this.imapSenha.set('');
    this.clientId.set(cliente.blingClientId ?? '');
    this.clientSecret.set('');
    avisarCamposPreenchidos(this.elemento.nativeElement);
  }
}
