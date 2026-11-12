import { Component, ElementRef, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { PrevisioService } from '../../core/previsio.service';
import { SimboloComponent } from '../../shared/simbolo/simbolo.component';

/**
 * "Esqueci minha senha": pede o link de redefinição por e-mail (POST /api/auth/esqueci-senha).
 * A resposta é sempre a mesma, para não revelar quais e-mails têm acesso.
 */
@Component({
  selector: 'app-esqueci-senha',
  imports: [FormsModule, RouterLink, SimboloComponent],
  templateUrl: './esqueci-senha.component.html',
  styleUrl: '../login/login.component.scss'
})
export class EsqueciSenhaComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly campoEmail = viewChild<ElementRef<HTMLInputElement>>('campoEmail');

  readonly ano = new Date().getFullYear();
  readonly email = signal('');
  readonly enviando = signal(false);
  readonly enviado = signal(false);
  readonly erro = signal('');

  constructor() {
    afterNextRender(() => {
      const campo = this.campoEmail();
      if (campo) {
        campo.nativeElement.focus();
      }
    });
  }

  enviar(): void {
    if (this.enviando()) {
      return;
    }
    const email = this.email().trim();
    if (email === '' || !email.includes('@')) {
      this.erro.set('Informe o e-mail de acesso.');
      return;
    }

    this.erro.set('');
    this.enviando.set(true);
    this.previsio.solicitarRedefinicaoSenha(email).subscribe({
      next: () => {
        this.enviando.set(false);
        this.email.set(email);
        this.enviado.set(true);
      },
      error: (resposta: HttpErrorResponse) => {
        this.enviando.set(false);
        if (resposta.status === 429) {
          this.erro.set('Muitas tentativas seguidas. Aguarde um minuto e tente de novo.');
        } else if (resposta.status === 0) {
          this.erro.set('Não foi possível falar com o servidor. Verifique se a API está no ar.');
        } else {
          this.erro.set('Não foi possível enviar agora. Tente de novo em instantes.');
        }
      }
    });
  }

  /** Volta ao formulário com o mesmo e-mail, para pedir outro link. */
  enviarDeNovo(): void {
    this.enviado.set(false);
  }
}
