import { Component, ElementRef, afterNextRender, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { avisarCamposPreenchidos } from '../../core/campos-preenchidos';
import { ClientesService } from '../../core/clientes.service';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { NovoUsuario, PERFIL_ADMIN, PERFIL_CLIENTE, Usuario } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';

/**
 * Cadastro de usuários (só admin): quem entra no painel e de qual cliente.
 */
@Component({
  selector: 'app-usuarios',
  imports: [FormsModule, DatePipe],
  templateUrl: './usuarios.component.html'
})
export class UsuariosComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly elemento = inject(ElementRef<HTMLElement>);

  readonly perfilAdmin = PERFIL_ADMIN;
  readonly perfilCliente = PERFIL_CLIENTE;
  readonly clientes = inject(ClientesService).lista;

  readonly usuarios = signal<Usuario[]>([]);
  readonly mensagem = signal('');
  readonly erro = signal('');

  // Novo usuário
  readonly nome = signal('');
  readonly email = signal('');
  readonly senha = signal('');
  readonly perfil = signal(PERFIL_CLIENTE);
  readonly clienteId = signal<number | null>(null);
  readonly enviando = signal(false);

  // Ações por linha
  readonly processandoId = signal<number | null>(null);
  readonly redefinindoId = signal<number | null>(null);
  readonly novaSenha = signal('');

  constructor() {
    this.carregar();
    afterNextRender(() => avisarCamposPreenchidos(this.elemento.nativeElement));
  }

  carregar(): void {
    this.previsio.listarUsuarios().subscribe({
      next: usuarios => this.usuarios.set(usuarios),
      error: resposta => this.erro.set(mensagemDaApi(resposta, 'Não foi possível carregar os usuários.'))
    });
  }

  nomeDoCliente(usuario: Usuario): string {
    if (usuario.clienteId === null) {
      return '—';
    }
    const cliente = this.clientes().find(item => item.id === usuario.clienteId);
    if (cliente) {
      return cliente.nome;
    }
    return 'Cliente ' + usuario.clienteId;
  }

  trocarPerfil(perfil: string): void {
    this.perfil.set(perfil);
    // O campo do cliente aparece ou some conforme o perfil
    avisarCamposPreenchidos(this.elemento.nativeElement);
  }

  adicionar(): void {
    const nome = this.nome().trim();
    const email = this.email().trim();
    const senha = this.senha();
    const perfil = this.perfil();

    if (nome === '' || email === '' || senha === '') {
      this.erro.set('Informe o nome, o e-mail e a senha.');
      return;
    }
    let clienteId: number | null = null;
    if (perfil === PERFIL_CLIENTE) {
      clienteId = this.clienteId();
      if (clienteId === null) {
        this.erro.set('Escolha o cliente do usuário.');
        return;
      }
    }

    const dados: NovoUsuario = { nome: nome, email: email, senha: senha, perfil: perfil, clienteId: clienteId };
    this.limparAvisos();
    this.enviando.set(true);
    this.previsio.criarUsuario(dados).subscribe({
      next: usuario => {
        this.enviando.set(false);
        const lista = [...this.usuarios(), usuario];
        lista.sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));
        this.usuarios.set(lista);
        this.nome.set('');
        this.email.set('');
        this.senha.set('');
        this.mensagem.set('Usuário cadastrado. Passe a senha provisória a ele.');
      },
      error: resposta => {
        this.enviando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível cadastrar o usuário.'));
      }
    });
  }

  alternarAtivo(usuario: Usuario): void {
    let sucesso = 'Usuário ativado.';
    if (usuario.ativo) {
      sucesso = 'Usuário desativado. As sessões abertas dele foram encerradas.';
    }
    this.alterar(usuario, { ativo: !usuario.ativo }, sucesso);
  }

  // ---------- redefinir senha na própria linha ----------

  abrirRedefinicao(usuario: Usuario): void {
    this.redefinindoId.set(usuario.id);
    this.novaSenha.set('');
  }

  cancelarRedefinicao(): void {
    this.redefinindoId.set(null);
  }

  salvarSenha(usuario: Usuario): void {
    if (this.novaSenha() === '') {
      this.erro.set('Informe a nova senha.');
      return;
    }
    this.alterar(usuario, { novaSenha: this.novaSenha() }, 'Senha redefinida. As sessões abertas do usuário foram encerradas.');
  }

  private alterar(usuario: Usuario, dados: { ativo?: boolean; novaSenha?: string }, sucesso: string): void {
    this.limparAvisos();
    this.processandoId.set(usuario.id);
    this.previsio.alterarUsuario(usuario.id, dados).subscribe({
      next: alterado => {
        this.processandoId.set(null);
        this.redefinindoId.set(null);
        this.usuarios.set(this.usuarios().map(item => {
          if (item.id === alterado.id) {
            return alterado;
          }
          return item;
        }));
        this.mensagem.set(sucesso);
      },
      error: resposta => {
        this.processandoId.set(null);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível alterar o usuário.'));
      }
    });
  }

  private limparAvisos(): void {
    this.erro.set('');
    this.mensagem.set('');
  }
}
