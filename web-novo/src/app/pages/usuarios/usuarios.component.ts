import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { avisarCamposPreenchidos } from '../../core/campos-preenchidos';
import { ClientesService } from '../../core/clientes.service';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { AlteracaoUsuario, NovoUsuario, PERFIL_ADMIN, PERFIL_CLIENTE, Usuario } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { MenuAcoesComponent } from '../../shared/menu-acoes/menu-acoes.component';
import { TAMANHO_PAGINA_PADRAO, paginar } from '../../core/paginacao';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';
import { BuscaComponent } from '../../shared/busca/busca.component';
import { ColunaOrdenavelComponent } from '../../shared/coluna-ordenavel/coluna-ordenavel.component';
import { Ordenacao, alternarOrdenacao, combinaComBusca, ordenarLista } from '../../core/ordenacao';

/**
 * Cadastro de usuários (só admin): quem entra no painel e de qual cliente.
 * O mesmo formulário do topo cadastra um usuário novo ou, depois de "Editar", altera o nome
 * e redefine a senha do escolhido (e-mail, perfil e cliente não mudam depois do cadastro).
 */
@Component({
  selector: 'app-usuarios',
  imports: [FormsModule, DatePipe, MenuAcoesComponent, CarregandoDirective, PaginacaoComponent, BuscaComponent, ColunaOrdenavelComponent],
  templateUrl: './usuarios.component.html'
})
export class UsuariosComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly elemento = inject(ElementRef<HTMLElement>);
  private readonly injector = inject(Injector);

  readonly perfilAdmin = PERFIL_ADMIN;
  readonly perfilCliente = PERFIL_CLIENTE;
  readonly clientes = inject(ClientesService).lista;

  readonly usuarios = signal<Usuario[]>([]);

  // Paginação da tabela (a API devolve a lista inteira)
  readonly pagina = signal(1);
  readonly tamanhoPagina = signal(TAMANHO_PAGINA_PADRAO);
  // Caixa de busca e ordenação por coluna, aplicadas aqui (a API devolve a lista inteira)
  readonly busca = signal('');
  readonly ordenacao = signal<Ordenacao | null>({ coluna: 'nome', decrescente: false });
  readonly usuariosFiltrados = computed(() => {
    const termo = this.busca();
    return this.usuarios().filter(item => combinaComBusca(termo, [item.nome, item.email, this.nomeDoCliente(item)]));
  });
  readonly usuariosOrdenados = computed(() => ordenarLista(this.usuariosFiltrados(), this.ordenacao(), {
    nome: item => item.nome,
    email: item => item.email,
    perfil: item => item.perfil,
    cliente: item => this.nomeDoCliente(item),
    situacao: item => item.ativo ? 0 : 1,
    acesso: item => item.ultimoAcessoEm
  }));
  readonly usuariosDaPagina = computed(() => paginar(this.usuariosOrdenados(), this.pagina(), this.tamanhoPagina()));
  readonly mensagem = signal('');
  readonly erro = signal('');

  // Campos do formulário (cadastro e edição)
  readonly nome = signal('');
  readonly email = signal('');
  readonly senha = signal('');
  readonly perfil = signal(PERFIL_CLIENTE);
  readonly clienteId = signal<number | null>(null);
  readonly enviando = signal(false);

  // Usuário sendo alterado no formulário; null = cadastro de um novo
  readonly editando = signal<Usuario | null>(null);

  // O formulário fica fechado e abre em "+ Novo usuário" ou "Editar"
  readonly formularioAberto = signal(false);

  // Ações por linha
  readonly processandoId = signal<number | null>(null);

  private readonly cartaoFormulario = viewChild<ElementRef<HTMLElement>>('cartaoFormulario');
  private readonly campoNome = viewChild<ElementRef<HTMLInputElement>>('campoNome');

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
    // O "change" disparado por avisarCamposPreenchidos chega aqui com o mesmo perfil;
    // sem esta saída, um chamava o outro sem parar (mais de mil eventos por segundo)
    if (perfil === this.perfil()) {
      return;
    }
    this.perfil.set(perfil);
    // O campo do cliente aparece ou some conforme o perfil
    avisarCamposPreenchidos(this.elemento.nativeElement);
  }

  /** Envio do formulário: altera o usuário em edição ou cadastra um novo. */
  salvar(): void {
    const usuario = this.editando();
    if (usuario !== null) {
      this.salvarEdicao(usuario);
    } else {
      this.adicionar();
    }
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

  // ---------- formulário do topo: novo usuário ou edição ----------

  /** "+ Novo usuário": abre o formulário vazio e leva o foco ao nome. */
  abrirNovo(): void {
    this.limparAvisos();
    this.limparFormulario();
    this.formularioAberto.set(true);
    this.focarFormulario();
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

  /** Preenche o formulário com o usuário e leva a tela até ele. */
  editar(usuario: Usuario): void {
    this.limparAvisos();
    this.editando.set(usuario);
    this.nome.set(usuario.nome);
    this.email.set(usuario.email);
    this.senha.set('');
    this.perfil.set(usuario.perfil);
    this.clienteId.set(usuario.clienteId);
    this.formularioAberto.set(true);
    this.focarFormulario();
  }

  estaEditando(usuario: Usuario): boolean {
    const editando = this.editando();
    return editando !== null && editando.id === usuario.id;
  }

  cancelarEdicao(): void {
    this.limparFormulario();
  }

  private salvarEdicao(usuario: Usuario): void {
    const nome = this.nome().trim();
    if (nome === '') {
      this.erro.set('Informe o nome do usuário.');
      return;
    }

    const dados: AlteracaoUsuario = { nome: nome };
    let sucesso = 'Usuário alterado.';
    // Senha em branco mantém a atual
    if (this.senha() !== '') {
      dados.novaSenha = this.senha();
      sucesso = 'Usuário alterado. Senha redefinida: as sessões abertas dele foram encerradas.';
    }
    this.alterar(usuario, dados, sucesso);
  }

  private alterar(usuario: Usuario, dados: AlteracaoUsuario, sucesso: string): void {
    this.limparAvisos();
    this.processandoId.set(usuario.id);
    this.previsio.alterarUsuario(usuario.id, dados).subscribe({
      next: alterado => {
        this.processandoId.set(null);
        this.usuarios.set(this.usuarios().map(item => {
          if (item.id === alterado.id) {
            return alterado;
          }
          return item;
        }));
        // Salvou o formulário de edição: volta ao cadastro de um novo
        const editando = this.editando();
        if (editando !== null && editando.id === alterado.id) {
          if (dados.nome !== undefined) {
            this.limparFormulario();
          } else {
            this.editando.set(alterado);
          }
        }
        this.mensagem.set(sucesso);
      },
      error: resposta => {
        this.processandoId.set(null);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível alterar o usuário.'));
      }
    });
  }

  // Também fecha o formulário: depois de salvar ou cancelar a tela volta para a lista
  private limparFormulario(): void {
    this.formularioAberto.set(false);
    this.editando.set(null);
    this.nome.set('');
    this.email.set('');
    this.senha.set('');
    this.perfil.set(PERFIL_CLIENTE);
    this.clienteId.set(null);
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
