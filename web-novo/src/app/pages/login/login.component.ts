import { Component, ElementRef, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { SessaoService } from '../../core/sessao.service';
import { SimboloComponent } from '../../shared/simbolo/simbolo.component';

/**
 * Tela de login: troca e-mail e senha por um token JWT na API (POST /api/auth/login).
 */
@Component({
  selector: 'app-login',
  imports: [FormsModule, SimboloComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly router = inject(Router);
  private readonly rota = inject(ActivatedRoute);
  private readonly sessao = inject(SessaoService);
  private readonly campoEmail = viewChild<ElementRef<HTMLInputElement>>('campoEmail');

  readonly ano = new Date().getFullYear();
  readonly email = signal('');
  readonly senha = signal('');
  readonly enviando = signal(false);
  readonly erro = signal('');
  readonly aviso = signal('');
  readonly senhaVisivel = signal(false);
  readonly capsLock = signal(false);

  readonly tipoSenha = computed(() => {
    if (this.senhaVisivel()) {
      return 'text';
    }
    return 'password';
  });

  readonly rotuloOlho = computed(() => {
    if (this.senhaVisivel()) {
      return 'Ocultar senha';
    }
    return 'Mostrar senha';
  });

  constructor() {
    afterNextRender(() => {
      const campo = this.campoEmail();
      if (campo) {
        campo.nativeElement.focus();
      }
    });

    if (this.rota.snapshot.queryParamMap.get('motivo') === 'expirou') {
      this.aviso.set('Sua sessão terminou. Entre novamente para continuar.');
    }
  }

  alternarSenha(): void {
    this.senhaVisivel.set(!this.senhaVisivel());
  }

  verificarCapsLock(evento: KeyboardEvent): void {
    // getModifierState não existe em eventos sintéticos de alguns navegadores antigos
    if (typeof evento.getModifierState === 'function') {
      this.capsLock.set(evento.getModifierState('CapsLock'));
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
