import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { ClientesService } from '../../core/clientes.service';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { Cliente } from '../../core/previsio.models';
import { SessaoService } from '../../core/sessao.service';

/**
 * Cadastro de clientes (só admin). Cada cliente é uma rede: tem a própria caixa de e-mail,
 * onde chegam os XMLs, e as suas empresas (CNPJs), cadastradas depois na tela Empresas.
 */
@Component({
  selector: 'app-clientes',
  imports: [FormsModule, RouterLink],
  templateUrl: './clientes.component.html'
})
export class ClientesComponent {
  private readonly clientes = inject(ClientesService);
  private readonly sessao = inject(SessaoService);
  private readonly router = inject(Router);

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

  verEmpresas(cliente: Cliente): void {
    this.sessao.escolherCliente(cliente.id);
    this.router.navigate(['/pages/empresas']);
  }
}
