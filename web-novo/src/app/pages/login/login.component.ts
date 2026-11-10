import { Component, DestroyRef, DOCUMENT, afterNextRender, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { SessaoService } from '../../core/sessao.service';

/**
 * Tela de login: troca e-mail e senha por um token JWT na API (POST /api/auth/login).
 */
@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.component.html'
})
export class LoginComponent {
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);
  private readonly rota = inject(ActivatedRoute);
  private readonly sessao = inject(SessaoService);

  readonly ano = new Date().getFullYear();
  readonly email = signal('');
  readonly senha = signal('');
  readonly cartaoVisivel = signal(false);
  readonly enviando = signal(false);
  readonly erro = signal('');
  readonly aviso = signal('');

  constructor() {
    const body = this.document.body;
    body.classList.add('login-page', 'off-canvas-sidebar');

    // Mesma animação do template: o cartão aparece logo depois de a página carregar
    afterNextRender(() => {
      setTimeout(() => this.cartaoVisivel.set(true), 700);
    });

    inject(DestroyRef).onDestroy(() => {
      body.classList.remove('login-page', 'off-canvas-sidebar');
    });

    if (this.rota.snapshot.queryParamMap.get('motivo') === 'expirou') {
      this.aviso.set('Sua sessão terminou. Entre novamente para continuar.');
    }
  }

  entrar(): void {
    if (this.enviando()) {
      return;
    }

    const email = this.email().trim();
    const senha = this.senha();
    if (email === '' || senha === '') {
      this.erro.set('Informe o e-mail e a senha.');
      return;
    }

    this.erro.set('');
    this.aviso.set('');
    this.enviando.set(true);

    this.sessao.entrar(email, senha).subscribe({
      next: () => {
        this.enviando.set(false);
        this.router.navigateByUrl(this.destinoDepoisDoLogin());
      },
      error: (resposta: HttpErrorResponse) => {
        this.enviando.set(false);
        this.senha.set('');
        this.erro.set(mensagemDeErro(resposta.status));
      }
    });
  }

  // Volta para a tela que pediu o login; só aceita telas do próprio painel
  private destinoDepoisDoLogin(): string {
    const retorno = this.rota.snapshot.queryParamMap.get('retorno');
    if (retorno && retorno.startsWith('/pages/') && !retorno.startsWith('/pages/login')) {
      return retorno;
    }
    return '/pages/painel';
  }
}

function mensagemDeErro(status: number): string {
  if (status === 401) {
    return 'E-mail ou senha inválidos.';
  }
  if (status === 429) {
    return 'Muitas tentativas seguidas. Aguarde um minuto e tente de novo.';
  }
  if (status === 0) {
    return 'Não foi possível falar com o servidor. Verifique se a API está no ar.';
  }
  return 'Não foi possível entrar agora. Tente de novo em instantes.';
}
