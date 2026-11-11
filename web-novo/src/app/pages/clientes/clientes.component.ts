import { Component, ElementRef, Injector, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { ClientesService } from '../../core/clientes.service';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { AlteracaoCliente, Cliente } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';
import { MenuAcoesComponent } from '../../shared/menu-acoes/menu-acoes.component';

/**
 * Cadastro de clientes (só admin). Cada cliente é uma rede: tem a própria caixa de e-mail,
 * onde chegam os XMLs, e as suas empresas (CNPJs), cadastradas depois na tela Empresas.
 * O mesmo formulário do topo cadastra um cliente novo ou, depois de "Editar", altera o escolhido.
 */
@Component({
  selector: 'app-clientes',
  imports: [FormsModule, RouterLink, MenuAcoesComponent],
  templateUrl: './clientes.component.html'
})
export class ClientesComponent {
  private readonly clientes = inject(ClientesService);
  private readonly sessao = inject(SessaoService);
  private readonly router = inject(Router);
  private readonly previsio = inject(PrevisioService);
  private readonly injector = inject(Injector);

  readonly lista = this.clientes.lista;
  readonly clienteExibido = this.sessao.clienteId;

  readonly nome = signal('');
  readonly imapHost = signal('imap.gmail.com');
  readonly imapPorta = signal<number | null>(993);
  readonly imapSsl = signal(true);
  readonly imapUsuario = signal('');
  readonly imapSenha = signal('');
  readonly enviando = signal(false);
  readonly erro = signal('');
  readonly mensagem = signal('');

  // Cliente sendo alterado no formulário; null = cadastro de um novo
  readonly editando = signal<Cliente | null>(null);

  private readonly cartaoFormulario = viewChild<ElementRef<HTMLElement>>('cartaoFormulario');
  private readonly campoNome = viewChild<ElementRef<HTMLInputElement>>('campoNome');

  /** Envio do formulário: altera o cliente em edição ou cadastra um novo. */
  salvar(): void {
    const cliente = this.editando();
    if (cliente !== null) {
      this.salvarEdicao(cliente);
    } else {
      this.adicionar();
    }
  }

  adicionar(): void {
    const nome = this.nome().trim();
    const host = this.imapHost().trim();
    const usuario = this.imapUsuario().trim();
    const senha = this.imapSenha();
    const porta = Number(this.imapPorta());

    if (nome === '') {
      this.erro.set('Informe o nome do cliente.');
      return;
    }
    // A caixa é obrigatória: sem ela o Previsio não lê as notas desse cliente
    if (host === '' || usuario === '' || senha.trim() === '') {
      this.erro.set('Informe o servidor, o e-mail e a senha da caixa dedicada.');
      return;
    }
    if (!Number.isInteger(porta) || porta < 1 || porta > 65535) {
      this.erro.set('Informe uma porta entre 1 e 65535 (no Gmail, 993).');
      return;
    }

    this.erro.set('');
    this.mensagem.set('');
    this.enviando.set(true);
    this.clientes.criar({
      nome: nome,
      imapHost: host,
      imapPorta: porta,
      imapSsl: this.imapSsl(),
      imapUsuario: usuario,
      imapSenha: senha
    }).subscribe({
      next: () => {
        // O cliente novo já está escolhido: o próximo passo é cadastrar as empresas dele
        this.router.navigate(['/pages/empresas'], { queryParams: { cliente: 'novo' } });
      },
      error: resposta => {
        this.enviando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível cadastrar o cliente.'));
      }
    });
  }

  // ---------- edição no formulário do topo ----------

  /** Preenche o formulário com o cliente e leva a tela até ele. A senha fica em branco (mantém a atual). */
  editar(cliente: Cliente): void {
    this.erro.set('');
    this.mensagem.set('');
    this.editando.set(cliente);
    this.nome.set(cliente.nome);
    this.imapHost.set(cliente.imapHost);
    this.imapPorta.set(cliente.imapPorta);
    this.imapSsl.set(cliente.imapSsl);
    this.imapUsuario.set(cliente.imapUsuario);
    this.imapSenha.set('');

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

  estaEditando(cliente: Cliente): boolean {
    const editando = this.editando();
    return editando !== null && editando.id === cliente.id;
  }

  cancelarEdicao(): void {
    this.limparFormulario();
  }

  private salvarEdicao(cliente: Cliente): void {
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

    this.erro.set('');
    this.mensagem.set('');
    this.enviando.set(true);
    this.previsio.alterarCliente(cliente.id, dados).subscribe({
      next: alterado => {
        this.enviando.set(false);
        this.clientes.atualizar(alterado);
        this.limparFormulario();
        this.mensagem.set('Cliente alterado.');
      },
      error: resposta => {
        this.enviando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível alterar o cliente.'));
      }
    });
  }

  private limparFormulario(): void {
    this.editando.set(null);
    this.nome.set('');
    this.imapHost.set('imap.gmail.com');
    this.imapPorta.set(993);
    this.imapSsl.set(true);
    this.imapUsuario.set('');
    this.imapSenha.set('');
  }

  verEmpresas(cliente: Cliente): void {
    this.sessao.escolherCliente(cliente.id);
    this.router.navigate(['/pages/empresas']);
  }
}
