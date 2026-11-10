import { Component, ElementRef, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { avisarCamposPreenchidos } from '../../core/campos-preenchidos';
import { ClientesService } from '../../core/clientes.service';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { AlteracaoCliente, Cliente, Empresa } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';

/**
 * Edição de um cliente (só admin): dados da caixa de e-mail, ativar/desativar e as empresas dele.
 */
@Component({
  selector: 'app-cliente-detalhe',
  imports: [FormsModule, RouterLink, DatePipe],
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
  readonly carregando = signal(true);

  readonly nome = signal('');
  readonly imapHost = signal('');
  readonly imapPorta = signal<number | null>(993);
  readonly imapSsl = signal(true);
  readonly imapUsuario = signal('');
  readonly imapSenha = signal('');

  readonly salvando = signal(false);
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

    this.previsio.listarEmpresasDoCliente(this.clienteId).subscribe({
      next: empresas => this.empresas.set(empresas),
      error: () => this.erro.set('Não foi possível carregar as empresas do cliente.')
    });
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

    const dados: AlteracaoCliente = {
      nome: nome,
      imapHost: host,
      imapPorta: porta,
      imapSsl: this.imapSsl(),
      imapUsuario: usuario
    };
    // Senha em branco mantém a atual
    if (this.imapSenha().trim() !== '') {
      dados.imapSenha = this.imapSenha();
    }

    this.enviar(dados, 'Cliente alterado.');
  }

  alternarAtivo(): void {
    const cliente = this.cliente();
    if (cliente === null) {
      return;
    }
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

  private enviar(dados: AlteracaoCliente, sucesso: string): void {
    this.erro.set('');
    this.mensagem.set('');
    this.salvando.set(true);
    this.previsio.alterarCliente(this.clienteId, dados).subscribe({
      next: cliente => {
        this.salvando.set(false);
        this.preencher(cliente);
        this.clientes.atualizar(cliente);
        this.mensagem.set(sucesso);
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
    avisarCamposPreenchidos(this.elemento.nativeElement);
  }
}
