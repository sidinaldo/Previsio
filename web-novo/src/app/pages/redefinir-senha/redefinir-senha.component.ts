import { Component, ElementRef, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { mensagemDaApi } from '../../core/mensagem-erro';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';
import { SimboloComponent } from '../../shared/simbolo/simbolo.component';

/** Mesmo mínimo da API (RegrasUsuario.TamanhoMinimoSenha). */
const TAMANHO_MINIMO_SENHA = 8;

/**
 * Nova senha pelo link do e-mail (/pages/redefinir-senha?token=...). Depois de salvar,
 * as sessões abertas caem e a pessoa entra de novo pelo login.
 */
@Component({
  selector: 'app-redefinir-senha',
  imports: [FormsModule, RouterLink, SimboloComponent],
  templateUrl: './redefinir-senha.component.html',
  styleUrl: '../login/login.component.scss'
})
export class RedefinirSenhaComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly sessao = inject(SessaoService);
  private readonly router = inject(Router);
  private readonly campoSenha = viewChild<ElementRef<HTMLInputElement>>('campoSenha');

  readonly ano = new Date().getFullYear();
  readonly tamanhoMinimo = TAMANHO_MINIMO_SENHA;
  readonly token = inject(ActivatedRoute).snapshot.queryParamMap.get('token') ?? '';

  readonly senha = signal('');
  readonly confirmacao = signal('');
  readonly senhaVisivel = signal(false);
  readonly enviando = signal(false);
  readonly erro = signal('');
  // O link expirou ou já foi usado: mostra o atalho para pedir outro
  readonly linkInvalido = signal(false);

  readonly tipoSenha = computed(() => (this.senhaVisivel() ? 'text' : 'password'));

  constructor() {
    afterNextRender(() => {
      const campo = this.campoSenha();
      if (campo) {
        campo.nativeElement.focus();
      }
    });
  }

  salvar(): void {
    if (this.enviando()) {
      return;
    }
    const senha = this.senha();
    if (senha.length < TAMANHO_MINIMO_SENHA) {
      this.erro.set('A senha precisa ter pelo menos ' + TAMANHO_MINIMO_SENHA + ' caracteres.');
      return;
    }
    if (senha !== this.confirmacao()) {
      this.erro.set('As duas senhas não são iguais.');
      return;
    }

    this.erro.set('');
    this.linkInvalido.set(false);
    this.enviando.set(true);
    this.previsio.redefinirSenha(this.token, senha).subscribe({
      next: () => {
        this.enviando.set(false);
        // Se este navegador estava logado, a sessão antiga não vale mais
        // (a navegação abaixo substitui a de sair(), levando o aviso de senha alterada)
        if (this.sessao.estaLogado()) {
          this.sessao.sair();
        }
        this.router.navigate(['/pages/login'], { queryParams: { motivo: 'senha-redefinida' } });
      },
      error: (resposta: HttpErrorResponse) => {
        this.enviando.set(false);
        if (resposta.status === 429) {
          this.erro.set('Muitas tentativas seguidas. Aguarde um minuto e tente de novo.');
          return;
        }
        if (resposta.status === 0) {
          this.erro.set('Não foi possível falar com o servidor. Verifique se a API está no ar.');
          return;
        }
        const mensagem = mensagemDaApi(resposta, 'Não foi possível salvar a nova senha.');
        this.erro.set(mensagem);
        this.linkInvalido.set(mensagem.includes('link'));
      }
    });
  }
}
