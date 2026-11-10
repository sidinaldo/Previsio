import { Component, DestroyRef, DOCUMENT, afterNextRender, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

/**
 * Tela de login: por enquanto só o visual.
 * A autenticação (JWT na API) entra numa etapa seguinte; hoje o botão apenas abre o painel.
 */
@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.component.html'
})
export class LoginComponent {
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);

  readonly ano = new Date().getFullYear();
  readonly email = signal('');
  readonly senha = signal('');
  readonly cartaoVisivel = signal(false);

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
  }

  entrar(): void {
    this.router.navigateByUrl('/pages/painel');
  }
}
