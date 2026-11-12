import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { SituacaoImplantacao, usuariosDoCliente } from '../../core/implantacao';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { Cliente, PERFIL_CLIENTE } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { TAMANHO_PAGINA_PADRAO, paginar } from '../../core/paginacao';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';

/** Mesmo mínimo da API (RegrasUsuario.TamanhoMinimoSenha). */
const TAMANHO_MINIMO_SENHA = 8;

/**
 * Etapa 4: o acesso do financeiro do cliente (perfil Cliente), que só enxerga esta rede.
 */
@Component({
  selector: 'app-etapa-usuario',
  imports: [FormsModule, CarregandoDirective, PaginacaoComponent],
  templateUrl: './etapa-usuario.component.html'
})
export class EtapaUsuarioComponent {
  private readonly previsio = inject(PrevisioService);

  readonly cliente = input.required<Cliente>();
  readonly situacao = input.required<SituacaoImplantacao>();
  readonly alterou = output<void>();
  readonly continuar = output<void>();

  readonly tamanhoMinimo = TAMANHO_MINIMO_SENHA;

  readonly nome = signal('');
  readonly email = signal('');
  readonly senha = signal('');
  readonly cadastrando = signal(false);
  readonly erro = signal('');
  readonly mensagem = signal('');

  readonly usuarios = computed(() => usuariosDoCliente(this.situacao()));

  // Paginação da tabela (a lista inteira vem da API)
  readonly pagina = signal(1);
  readonly tamanhoPagina = signal(TAMANHO_PAGINA_PADRAO);
  readonly usuariosDaPagina = computed(() => paginar(this.usuarios(), this.pagina(), this.tamanhoPagina()));

  cadastrar(): void {
    const nome = this.nome().trim();
    const email = this.email().trim();
    const senha = this.senha();
    if (nome === '' || email === '' || senha === '') {
      this.erro.set('Informe o nome, o e-mail e a senha.');
      return;
    }
    if (senha.length < TAMANHO_MINIMO_SENHA) {
      this.erro.set('A senha precisa ter pelo menos ' + TAMANHO_MINIMO_SENHA + ' caracteres.');
      return;
    }

    this.erro.set('');
    this.mensagem.set('');
    this.cadastrando.set(true);
    this.previsio.criarUsuario({
      nome: nome,
      email: email,
      senha: senha,
      perfil: PERFIL_CLIENTE,
      clienteId: this.cliente().id
    }).subscribe({
      next: () => {
        this.cadastrando.set(false);
        this.nome.set('');
        this.email.set('');
        this.senha.set('');
        this.mensagem.set('Usuário cadastrado. Passe a senha provisória a ele.');
        this.alterou.emit();
      },
      error: resposta => {
        this.cadastrando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível cadastrar o usuário.'));
      }
    });
  }
}
