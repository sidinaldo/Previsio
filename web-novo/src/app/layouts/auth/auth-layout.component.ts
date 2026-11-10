import { Component, DestroyRef, DOCUMENT, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

/**
 * Layout de tela cheia (sem menu lateral), usado pela tela de login.
 */
@Component({
  selector: 'app-auth-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './auth-layout.component.html'
})
export class AuthLayoutComponent {
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);

  readonly menuCelularAberto = signal(false);

  constructor() {
    this.router.events
      .pipe(filter(evento => evento instanceof NavigationEnd), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.fecharMenuCelular());
  }

  alternarMenuCelular(): void {
    if (this.menuCelularAberto()) {
      this.fecharMenuCelular();
    } else {
      this.document.body.classList.add('nav-open');
      this.menuCelularAberto.set(true);
    }
  }

  private fecharMenuCelular(): void {
    this.document.body.classList.remove('nav-open');
    this.menuCelularAberto.set(false);
  }
}
