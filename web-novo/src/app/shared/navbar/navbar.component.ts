import { Component, DestroyRef, DOCUMENT, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs';

import { tituloDaRota } from '../sidebar/menu';

/**
 * Barra do topo do layout admin.
 * Reproduz o comportamento do template sem jQuery:
 * - botão para minimizar o menu lateral (classe "sidebar-mini" no body)
 * - botão do celular que abre o menu (classe "nav-open" + camada escura)
 */
@Component({
  selector: 'app-navbar',
  imports: [RouterLink],
  templateUrl: './navbar.component.html'
})
export class NavbarComponent {
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);

  readonly titulo = signal(tituloDaRota(this.router.url));
  readonly menuCelularAberto = signal(false);

  private camada: HTMLElement | null = null;

  constructor() {
    this.router.events
      .pipe(filter(evento => evento instanceof NavigationEnd), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => {
        this.titulo.set(tituloDaRota(this.router.url));
        this.fecharMenuCelular();
      });
  }

  minimizarMenu(): void {
    const body = this.document.body;
    if (body.classList.contains('sidebar-mini')) {
      body.classList.remove('sidebar-mini');
    } else {
      body.classList.add('sidebar-mini');
    }
    // O template simula um resize para os gráficos se ajustarem à nova largura
    this.simularRedimensionamento();
  }

  alternarMenuCelular(): void {
    if (this.menuCelularAberto()) {
      this.fecharMenuCelular();
    } else {
      this.abrirMenuCelular();
    }
  }

  private abrirMenuCelular(): void {
    this.document.body.classList.add('nav-open');
    this.menuCelularAberto.set(true);

    const camada = this.document.createElement('div');
    camada.className = 'close-layer';
    camada.addEventListener('click', () => this.fecharMenuCelular());

    const painel = this.document.querySelector('.main-panel');
    if (painel) {
      painel.appendChild(camada);
    }
    setTimeout(() => camada.classList.add('visible'), 100);
    this.camada = camada;
  }

  private fecharMenuCelular(): void {
    this.document.body.classList.remove('nav-open');
    this.menuCelularAberto.set(false);

    if (this.camada) {
      const camada = this.camada;
      camada.classList.remove('visible');
      setTimeout(() => camada.remove(), 400);
      this.camada = null;
    }
  }

  private simularRedimensionamento(): void {
    const intervalo = setInterval(() => window.dispatchEvent(new Event('resize')), 180);
    setTimeout(() => clearInterval(intervalo), 1000);
  }
}
